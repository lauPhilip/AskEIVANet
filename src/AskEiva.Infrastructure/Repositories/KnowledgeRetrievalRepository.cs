using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
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
                SoftwareReleaseNode(
                  limit: {{limit}}
                  hybrid: { query: "{{userQuery}}", alpha: 0.5 }
                ) {
                  group_category
                  product
                  release_type
                  version
                  full_version_title
                  metadata_note
                  content_chunk
                  ref_tickets
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
                    float.TryParse(node.GetProperty("_additional").GetProperty("score").GetRawText(), out var score);
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
                    float.TryParse(node.GetProperty("_additional").GetProperty("score").GetRawText(), out var score);
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

            // 3. Parse Release Notes data blocks dynamically
            if (root.TryGetProperty("SoftwareReleaseNode", out var releaseNodes))
            {
                foreach (var node in releaseNodes.EnumerateArray())
                {
                    float.TryParse(node.GetProperty("_additional").GetProperty("score").GetRawText(), out var score);
                    string product = node.GetProperty("product").GetString() ?? "Product Note";
                    string version = node.GetProperty("version").GetString() ?? "";
                    string header = node.GetProperty("section_header").GetString() ?? "Release Spec";
                    string note = node.TryGetProperty("metadata_note", out var nProp) ? nProp.GetString() ?? string.Empty : string.Empty;

                    matches.Add(new RetrievalMatch(
                        SourceId: node.TryGetProperty("ref_tickets", out var tProp) ? tProp.GetString() ?? string.Empty : string.Empty,
                        Title: $"[{product} v{version}] {header}",
                        Content: node.GetProperty("content_chunk").GetString() ?? string.Empty,
                        SourceUrl: "https://download.eiva.com/#",
                        ConfidenceScore: score,
                        SourceType: "ReleaseNote",
                        ImageUrls: new(),
                        ProductContext: product,
                        VersionContext: version
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

public async Task<IEnumerable<GraphContextChain>> SearchGraphTriplesAsync(string userQuery, int limit)
    {
        var url = "v1/graphql";
        var query = new
        {
            query = $$"""
            {
              Get {
                GraphContextChain(
                  limit: {{limit}}
                  hybrid: { query: "{{userQuery}}", alpha: 0.5 }
                ) {
                  ticket_id
                  ticket_title
                  main_product_context
                  scenario_type
                  shared_path_chain
                  predicates
                  environmental_context_summary
                  confidence_score
                }
              }
            }
            """
        };

        try
        {
            var response = await _httpClient.PostAsync(url, new StringContent(JsonSerializer.Serialize(query), Encoding.UTF8, "application/json"));
            if (!response.IsSuccessStatusCode) return Enumerable.Empty<GraphContextChain>();

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var meshes = new List<GraphContextChain>();

            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("Get", out var get) &&
                get.TryGetProperty("GraphContextChain", out var nodes))
            {
                foreach (var node in nodes.EnumerateArray())
                {
                    var predicatesList = new List<string>();
                    if (node.TryGetProperty("predicates", out var predProp) && predProp.ValueKind == JsonValueKind.Array)
                    {
                        predicatesList = predProp.EnumerateArray().Select(p => p.GetString() ?? string.Empty).ToList();
                    }

                    meshes.Add(new GraphContextChain
                    {
                        TicketId = node.GetProperty("ticket_id").GetString() ?? string.Empty,
                        TicketTitle = node.GetProperty("ticket_title").GetString() ?? string.Empty,
                        MainProductContext = node.GetProperty("main_product_context").GetString() ?? string.Empty,
                        ScenarioType = node.GetProperty("scenario_type").GetString() ?? string.Empty,
                        SharedPathChain = node.GetProperty("shared_path_chain").GetString() ?? string.Empty,
                        Predicates = predicatesList,
                        EnvironmentalContextSummary = node.GetProperty("environmental_context_summary").GetString() ?? string.Empty,
                        ConfidenceScore = node.GetProperty("confidence_score").GetInt32()
                    });
                }
            }
            return meshes;
        }
        catch
        {
            return Enumerable.Empty<GraphContextChain>();
        }
    }

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

public async Task<int> GetDistinctSourceCountAsync(string className, string propertyName)
{
    // 💡 THE MEMORY-SAFE STREAMING PASS: We request ONLY the tracking property (e.g., source_id).
    // Because we leave out the massive 'content' and vector fields, the payload is incredibly tiny.
    // We set a high limit to capture all chunks in a single fast, timeout-safe network hop.
    var gqlQuery = $$"""
    {
      Get {
        {{className}}(limit: 50000) {
          {{propertyName}}
        }
      }
    }
    """;

try
    {
        var response = await _httpClient.PostAsJsonAsync("v1/graphql", new { query = gqlQuery });
        if (!response.IsSuccessStatusCode) return 0;

        using var jsonDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        
        if (jsonDoc.RootElement.TryGetProperty("data", out var data) &&
            data.TryGetProperty("Get", out var get) &&
            get.TryGetProperty(className, out var chunkArray) &&
            chunkArray.ValueKind == JsonValueKind.Array)
        {
            var uniqueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Regex pattern to extract the KB number prefix: Match starts with kb/KB, then digits, followed by an underscore
            // This captures "Kb_43000623566_" or "kb_43000756150_" and discards the trailing "part_X"
            var kbPattern = new Regex(@"^(kb_\d+_)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            foreach (var chunk in chunkArray.EnumerateArray())
            {
                if (chunk.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
                {
                    string rawId = prop.GetString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(rawId)) continue;

                    if (className == "DocumentLibrary")
                    {
                        // 💡 EXTRACT PARENT DOCUMENT KEY:
                        // Look for the clean "kb_number_" prefix pattern matching your naming rule
                        var match = kbPattern.Match(rawId);
                        if (match.Success)
                        {
                            // 💡 FIXED: Uses standard .ToLowerInvariant() to keep cross-platform strings perfectly clean
                            uniqueIds.Add(match.Value.ToLowerInvariant()); 
                        }
                        else
                        {
                            // Fallback just in case a manual document entry doesn't use the 'part' suffix structure
                            uniqueIds.Add(rawId.ToLowerInvariant());
                        }
                    }
                    else
                    {
                        // Standard handling for support tickets (e.g., "FD-81488")
                        uniqueIds.Add(rawId);
                    }
                }
            }

            Console.WriteLine($"[Application Telemetry Engine] Cleaned {chunkArray.GetArrayLength()} chunks for {className}. Unique Parents Found: {uniqueIds.Count}");
            return uniqueIds.Count;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Telemetry Aggregation Error] Failed matching custom text filters for {className}: {ex.Message}");
    }

    return 0;
}

    public async Task<JsonElement> GetRawInteractionLogsAsync(int limit)
    {
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
                timestamp = DateTime.UtcNow.ToString("o")
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

public async Task BatchIngestReleaseNodesAsync(IEnumerable<SoftwareReleaseNode> nodes)
    {
        var url = "v1/batch/objects";
        
        // Formats your raw entity items into Weaviate properties records
        var allBatchObjects = nodes.Select(node => new
        {
            @class = "SoftwareReleaseNode",
            properties = new
            {
                group_category = node.GroupCategory,
                product = node.Product,
                release_type = node.ReleaseType,
                version = node.Version,
                full_version_title = node.FullVersionTitle,
                metadata_note = node.MetadataNote,
                release_date = node.ReleaseDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                section_header = node.SectionHeader,
                content_chunk = node.ContentChunk,
                ref_tickets = node.RefTickets
            }
        }).ToList();

        // 💡 FIXED: Split into chunks of 30 nodes to comply with Mistral's internal embedding limits
        const int MaxMistralBatchSize = 30;
        int totalIngestedCount = 0;

        for (int i = 0; i < allBatchObjects.Count; i += MaxMistralBatchSize)
        {
            var currentChunkPartition = allBatchObjects.Skip(i).Take(MaxMistralBatchSize).ToList();
            var payload = new { objects = currentChunkPartition };

            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, payload);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Weaviate Segment Error] Batch request aborted with status code: {response.StatusCode}");
                    continue;
                }

                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    int segmentSuccessCount = 0;
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.TryGetProperty("result", out var res) && res.TryGetProperty("errors", out var err))
                        {
                            Console.WriteLine($"[Weaviate Batch Object Rejection]: {err.GetRawText()}");
                        }
                        else
                        {
                            segmentSuccessCount++;
                        }
                    }
                    totalIngestedCount += segmentSuccessCount;
                    Console.WriteLine($"[Weaviate Ingestion Segment] Successfully vectorized {segmentSuccessCount}/{currentChunkPartition.Count} items.");
                }
                
                // Add a brief 100ms backoff sleep cycle to protect your Mistral API key tier from hitting rate-limits
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Infrastructure Batch Error] Processing track segment collapsed: {ex.Message}");
            }
        }

        Console.WriteLine($"\n[Weaviate Ingestion Engine] Task Finished! Total of {totalIngestedCount} out of {allBatchObjects.Count} records indexed successfully into the cluster.\n");
    }

public async Task<bool> DoesProductVersionExistAsync(string product, string version)
    {
        var url = "v1/graphql";
        
        // 💡 PERFECT MATCHING: Uses a GraphQL structural 'where' operator filtering pass
        var query = new
        {
            query = $$"""
            {
              Get {
                SoftwareReleaseNode(
                  limit: 1
                  where: {
                    operator: And,
                    operands: [
                      { path: ["product"], operator: Equal, valueText: "{{product}}" },
                      { path: ["version"], operator: Equal, valueText: "{{version}}" }
                    ]
                  }
                ) {
                  version
                }
              }
            }
            """
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, query);
            if (!response.IsSuccessStatusCode) return false;

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("Get", out var get) &&
                get.TryGetProperty("SoftwareReleaseNode", out var nodesArr) &&
                nodesArr.ValueKind == JsonValueKind.Array)
            {
                return nodesArr.GetArrayLength() > 0;
            }
        }
        catch { }
        return false;
    }

public async Task<List<TechnicalContextSearchResult>> SearchTechnicalContextAsync(string ticketId, int maxResults)
    {
        var results = new List<TechnicalContextSearchResult>();

        try
        {
            // 💡 FIXED: Using Weaviate's nearObject filter to execute cross-collection lookups 
            // using the ticket's server-side vector reference!
            var graphQlQuery = $$"""
            {
              Get {
                DocumentLibrary(
                  nearObject: { id: "{{ticketId}}" }
                  limit: {{maxResults}}
                ) {
                  _additional { id }
                  content
                }
                SoftwareReleaseNode(
                  nearObject: { id: "{{ticketId}}" }
                  limit: {{maxResults}}
                ) {
                  _additional { id }
                  content_chunk
                }
              }
            }
            """;

            var requestPayload = new { query = graphQlQuery };
            var response = await _httpClient.PostAsJsonAsync("v1/graphql", requestPayload);
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Weaviate HTTP Failure] GraphQL returned status: {response.StatusCode}");
                return results;
            }

            using var jsonDocument = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var root = jsonDocument.RootElement;

            if (root.TryGetProperty("data", out var dataNode) && dataNode.TryGetProperty("Get", out var getCollection))
            {
                // Map elements pulled from DocumentLibrary
                if (getCollection.TryGetProperty("DocumentLibrary", out var docArray) && docArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in docArray.EnumerateArray())
                    {
                        string idStr = elem.GetProperty("_additional").GetProperty("id").GetString() ?? Guid.NewGuid().ToString();
                        results.Add(new TechnicalContextSearchResult
                        {
                            Id = Guid.Parse(idStr),
                            Content = elem.GetProperty("content").GetString() ?? string.Empty,
                            CollectionName = "DocumentLibrary",
                            SuggestedEdgeType = "EXPLAINS_FEATURE"
                        });
                    }
                }

                // Map elements pulled from SoftwareReleaseNode
                if (getCollection.TryGetProperty("SoftwareReleaseNode", out var releaseArray) && releaseArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in releaseArray.EnumerateArray())
                    {
                        string idStr = elem.GetProperty("_additional").GetProperty("id").GetString() ?? Guid.NewGuid().ToString();
                        results.Add(new TechnicalContextSearchResult
                        {
                            Id = Guid.Parse(idStr),
                            Content = elem.GetProperty("content_chunk").GetString() ?? string.Empty,
                            CollectionName = "SoftwareReleaseNode",
                            SuggestedEdgeType = "ACCUSES_BUG"
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Infrastructure Graph Search Failure] Direct HTTP stream faulted: {ex.Message}");
        }

        return results.OrderBy(x => Guid.NewGuid()).Take(maxResults).ToList();
    }
}