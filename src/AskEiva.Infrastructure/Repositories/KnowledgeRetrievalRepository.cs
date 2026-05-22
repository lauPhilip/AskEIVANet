using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AskEiva.Domain.Repositories;
using AskEiva.Domain.ValueObjects;
using AskEiva.Domain.Entities;

namespace AskEiva.Infrastructure.Repositories;

public class KnowledgeRetrievalRepository : IKnowledgeRetrievalRepository
{
    private readonly HttpClient _httpClient;

    public KnowledgeRetrievalRepository(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<RetrievalMatch>> SearchSemanticChunksAsync(string userQuery, int limit)
    {
        var url = "v1/graphql";
        
        var query = new
        {
            query = $$"""
            {
              Get {
                KnowledgeNode(
                  limit: {{limit}}
                  hybrid: { query: "{{userQuery}}", alpha: 0.5 }
                ) {
                  source_id
                  subject
                  content
                  url
                  _additional { score }
                }
                DocumentLibrary(
                  limit: {{limit}}
                  hybrid: { query: "{{userQuery}}", alpha: 0.5 }
                ) {
                  document_id
                  title
                  content
                  url
                  _additional { score }
                }
              }
            }
            """
        };

        try
        {
            var response = await _httpClient.PostAsync(url, new StringContent(JsonSerializer.Serialize(query), Encoding.UTF8, "application/json"));
            if (!response.IsSuccessStatusCode) return Enumerable.Empty<RetrievalMatch>();

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var matches = new List<RetrievalMatch>();
            var root = doc.RootElement.GetProperty("data").GetProperty("Get");

            // 1. Parse ticket-based KnowledgeNodes
            if (root.TryGetProperty("KnowledgeNode", out var ticketNodes))
            {
                foreach (var node in ticketNodes.EnumerateArray())
                {
                    var additional = node.GetProperty("_additional");
                    var scoreElement = additional.GetProperty("score");
                    
                    var scoreStr = scoreElement.ValueKind == JsonValueKind.Number 
                        ? scoreElement.GetRawText() 
                        : scoreElement.GetString() ?? "0";
                    float.TryParse(scoreStr, out var score);

                    matches.Add(new RetrievalMatch(
                        SourceId: node.GetProperty("source_id").GetString() ?? string.Empty,
                        Title: node.GetProperty("subject").GetString() ?? "Technical Excerpt",
                        Content: node.GetProperty("content").GetString() ?? string.Empty,
                        SourceUrl: node.GetProperty("url").GetString() ?? string.Empty,
                        ConfidenceScore: score,
                        SourceType: "Ticket",
                        ImageUrls: new()
                    ));
                }
            }

            // 2. Parse documentation-based DocumentLibraries
            if (root.TryGetProperty("DocumentLibrary", out var docNodes))
            {
                foreach (var node in docNodes.EnumerateArray())
                {
                    var additional = node.GetProperty("_additional");
                    var scoreElement = additional.GetProperty("score");
                    
                    var scoreStr = scoreElement.ValueKind == JsonValueKind.Number 
                        ? scoreElement.GetRawText() 
                        : scoreElement.GetString() ?? "0";
                    float.TryParse(scoreStr, out var score);

                    matches.Add(new RetrievalMatch(
                        SourceId: node.TryGetProperty("document_id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty,
                        Title: node.TryGetProperty("title", out var titleProp) ? (titleProp.GetString() ?? "Documentation Article") : "Documentation Article",
                        Content: node.GetProperty("content").GetString() ?? string.Empty,
                        SourceUrl: node.GetProperty("url").GetString() ?? string.Empty,
                        ConfidenceScore: score,
                        SourceType: "Documentation",
                        ImageUrls: new()
                    ));
                }
            }

            return matches.OrderByDescending(m => m.ConfidenceScore);
        }
        catch
        {
            return Enumerable.Empty<RetrievalMatch>();
        }
    }

    public async Task<IEnumerable<KnowledgeTriple>> SearchGraphTriplesAsync(string userQuery, int limit)
    {
        var url = "v1/graphql";
        
        var query = new
        {
            // 💡 FIXED: Aligned property search criteria field to use 'evidence_id' so it syncs cleanly with your saved schemas
            query = $$"""
            {
              Get {
                EntityGraph(
                  limit: {{limit}}
                  hybrid: { query: "{{userQuery}}", alpha: 0.3 }
                ) {
                  subject
                  predicate
                  object
                  evidence_id
                }
              }
            }
            """
        };

        try
        {
            var response = await _httpClient.PostAsync(url, new StringContent(JsonSerializer.Serialize(query), Encoding.UTF8, "application/json"));
            if (!response.IsSuccessStatusCode) return Enumerable.Empty<KnowledgeTriple>();

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var triples = new List<KnowledgeTriple>();

            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("Get", out var get) &&
                get.TryGetProperty("EntityGraph", out var nodes))
            {
                foreach (var node in nodes.EnumerateArray())
                {
                    triples.Add(new KnowledgeTriple
                    {
                        Subject = node.GetProperty("subject").GetString() ?? string.Empty,
                        Predicate = node.GetProperty("predicate").GetString() ?? string.Empty,
                        Object = node.GetProperty("object").GetString() ?? string.Empty,
                        SourceTicketId = node.GetProperty("evidence_id").GetString() ?? string.Empty
                    });
                }
            }
            return triples;
        }
        catch
        {
            return Enumerable.Empty<KnowledgeTriple>();
        }
    }

    // =================================================================================
    // 💡 SYSTEM UNMOCKED TELEMETRY INTEGRATIONS (NEW PROPERTIES ADDED FOR DASHBOARD)
    // =================================================================================

    public async Task<int> GetTotalClassCountAsync(string className)
    {
        var jsonQuery = new { query = $"{{ Aggregate {{ {className} {{ meta {{ count }} }} }} }}" };
        try
        {
            var response = await _httpClient.PostAsJsonAsync("v1/graphql", jsonQuery);
            if (!response.IsSuccessStatusCode) return 0;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("Aggregate", out var agg) &&
                agg.TryGetProperty(className, out var classArr) &&
                classArr.ValueKind == JsonValueKind.Array && classArr.GetArrayLength() > 0)
            {
                var meta = classArr[0].GetProperty("meta");
                return meta.GetProperty("count").GetInt32();
            }
        }
        catch { }
        return 0;
    }

    public async Task<int> GetDistinctSourceCountAsync(string className, string groupProperty)
    {
        var jsonQuery = new { query = $"{{ Aggregate {{ {className}(groupBy:[\"{groupProperty}\"]) {{ meta {{ count }} }} }} }}" };
        try
        {
            var response = await _httpClient.PostAsJsonAsync("v1/graphql", jsonQuery);
            if (!response.IsSuccessStatusCode) return 0;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("Aggregate", out var agg) &&
                agg.TryGetProperty(className, out var classArr) &&
                classArr.ValueKind == JsonValueKind.Array)
            {
                return classArr.GetArrayLength();
            }
        }
        catch { }
        return 0;
    }

    public async Task<JsonElement> GetRawInteractionLogsAsync(int limit)
    {
        // 💡 PERFECT MATCH: Queries exactly what you have stored in Weaviate's log matrix
        var jsonQuery = new { query = $"{{ Get {{ InteractionLog(limit: {limit}) {{ query answer was_successful timestamp }} }} }}" };
        try
        {
            var response = await _httpClient.PostAsJsonAsync("v1/graphql", jsonQuery);
            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(jsonString);
                return doc.RootElement.Clone();
            }
        }
        catch { }
        return default;
    }

    //  Persists live interactions directly back to Weaviate matching your database schema
    public async Task LogInteractionAsync(string query, string answer, bool wasSuccessful)
    {
        var url = "v1/objects";

        var payload = new
        {
            @class = "InteractionLog",
            properties = new
            {
                query = query,
                answer = answer,
                was_successful = wasSuccessful,
                timestamp = DateTime.UtcNow.ToString("o") // ISO 8601 formatting configuration
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, payload);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Weaviate Telemetry Error] Failed to write active interaction log: {ex.Message}");
        }
    }

    // =================================================================================
    // 💡 SOFTWARE RELEASE BATCH INGESTION MATRIX PLUG-IN
    // =================================================================================
    
    public async Task BatchIngestReleaseNodesAsync(IEnumerable<SoftwareReleaseNode> nodes)
    {
        var url = "v1/batch/objects";
        
        var batchObjects = nodes.Select(node => new
        {
            @class = "SoftwareReleaseNode",
            properties = new
            {
                product = node.Product,
                version = node.Version,
                release_date = node.ReleaseDate.ToString("o"),
                section_header = node.SectionHeader,
                content_chunk = node.ContentChunk,
                ref_tickets = node.RefTickets
            }
        }).ToList();

        var payload = new { objects = batchObjects };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, payload);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Infrastructure Batch Error] Failed pushing release nodes to database: {ex.Message}");
        }
    }
}