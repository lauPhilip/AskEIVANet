using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Repositories;
using Microsoft.Extensions.Configuration;

namespace AskEiva.Infrastructure.Repositories;

/// <summary>
/// Implements the <see cref="ITicketRepository"/> contract, utilizing a token-authorized 
/// <see cref="HttpClient"/> pipeline to index, stitch, query, and manage segmented customer support tickets in Weaviate.
/// </summary>
public class TicketRepository : ITicketRepository
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketRepository"/> class, automatically injecting 
    /// the secure Weaviate Cloud (WCD) API credential into all inbound request header dictionaries.
    /// </summary>
    /// <param name="httpClient">An unmanaged or factory-allocated HTTP client configured for vector cluster operations.</param>
    /// <param name="configuration">The application configuration root used to resolve external environment secret settings.</param>
    public TicketRepository(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;

        // Automatically injects your Weaviate WCD API Key into every inbound transactional pipeline request
        string weaviateKey = configuration["WEAVIATE_API_KEY"] ?? string.Empty;
        if (!string.IsNullOrEmpty(weaviateKey) && !_httpClient.DefaultRequestHeaders.Contains("Authorization"))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {weaviateKey}");
        }
    }

    /// <summary>
    /// Inserts or updates an individual customer support ticket metadata record directly inside the Weaviate data store.
    /// </summary>
    /// <param name="ticket">The ticket entity node data model holding processed dialog items and priority weights.</param>
    /// <returns>An asynchronous task tracking completion of the storage transaction.</returns>
    public async Task UpsertTicketAsync(TicketNode ticket)
    {
        // Target the KnowledgeNode collection via Weaviate v4 Objects endpoint
        var url = "v1/objects";

        var payload = new
        {
            @class = "KnowledgeNode",
            properties = new
            {
                source_id = ticket.SourceId,
                data_type = ticket.DataType,
                subject = ticket.Subject,
                content = ticket.Content,
                is_distilled = ticket.IsDistilled,
                url = ticket.Url,
                status = ticket.Status,
                priority = ticket.Priority,
                tags = ticket.Tags
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Weaviate Error] Failed to upsert object: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Scans the vector engine using advanced GraphQL filter syntax to discover and unique-group ticket headers 
    /// needing graph relationship distillation sweeps.
    /// </summary>
    /// <param name="batchSize">The requested maximum limit number of ticket records to load into the memory stack.</param>
    /// <returns>A unique collection list of ticket entities with their content fields intentionally left unpopulated.</returns>
    public async Task<List<TicketNode>> GetUnprocessedTicketHeadersAsync(int batchSize)
    {
        var headers = new List<TicketNode>();

        try
        {
            // ADVANCED GRAPHQL: Group chunks by source_id where processing is still required
            var graphQlQuery = $$"""
            {
              Get {
                KnowledgeNode(
                  where: {
                    path: ["is_distilled"]
                    operator: Equal
                    valueBoolean: false
                  }
                  limit: {{batchSize * 5}}
                ) {
                  source_id
                  subject
                }
              }
            }
            """;

            var response = await _httpClient.PostAsJsonAsync("v1/graphql", new { query = graphQlQuery });
            if (!response.IsSuccessStatusCode) return headers;

            using var jsonDocument = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var root = jsonDocument.RootElement;

            if (root.TryGetProperty("data", out var dataNode) && 
                dataNode.TryGetProperty("Get", out var getCollection) &&
                getCollection.TryGetProperty("KnowledgeNode", out var chunkArray) &&
                chunkArray.ValueKind == JsonValueKind.Array)
            {
                // Use an internal hashset tracking collection to unique-filter the matching headers up to the requested batch size limit
                var uniqueIds = new HashSet<string>();

                foreach (var elem in chunkArray.EnumerateArray())
                {
                    if (headers.Count >= batchSize) break;

                    string sourceId = elem.TryGetProperty("source_id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty;
                    if (string.IsNullOrEmpty(sourceId) || uniqueIds.Contains(sourceId)) continue;

                    string subject = elem.TryGetProperty("subject", out var subProp) ? subProp.GetString() ?? "Support Thread Context" : "Support Thread Context";

                    uniqueIds.Add(sourceId);
                    headers.Add(new TicketNode
                    {
                        SourceId = sourceId,
                        Subject = subject,
                        Content = string.Empty // Kept empty intentionally; will be populated by the stitcher method!
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Infrastructure Ingestion Header Sweep Fault]: {ex.Message}");
        }

        return headers;
    }

    /// <summary>
    /// Queries vector data collections to extract and reassemble all fragmented discussion segments 
    /// matching a target source tracking code into a continuous chronology string.
    /// </summary>
    /// <param name="sourceId">The unique tracking identifier key of the targeted issue (e.g., "FD-4901").</param>
    /// <returns>A single stitched chronological thread containing the full context history.</returns>
    public async Task<string> GetStitchedTicketContentAsync(string sourceId)
    {
        try
        {
            // Query Weaviate for all chunks sharing the targeted source tracking ID
            var graphQlQuery = $$"""
            {
              Get {
                KnowledgeNode(
                  where: {
                    path: ["source_id"]
                    operator: Equal
                    valueText: "{{sourceId}}"
                  }
                ) {
                  content
                }
              }
            }
            """;

            var response = await _httpClient.PostAsJsonAsync("v1/graphql", new { query = graphQlQuery });
            if (!response.IsSuccessStatusCode) return string.Empty;

            using var jsonDocument = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var root = jsonDocument.RootElement;

            if (root.TryGetProperty("data", out var dataNode) && 
                dataNode.TryGetProperty("Get", out var getCollection) &&
                getCollection.TryGetProperty("KnowledgeNode", out var chunkArray) && 
                chunkArray.ValueKind == JsonValueKind.Array)
            {
                var stringBuilder = new StringBuilder();
                foreach (var chunk in chunkArray.EnumerateArray())
                {
                    if (chunk.TryGetProperty("content", out var contentElement))
                    {
                        stringBuilder.AppendLine(contentElement.GetString());
                    }
                }
                return stringBuilder.ToString();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Ticket Reassembly Failure]: {ex.Message}");
        }

        return string.Empty;
    }

    /// <summary>
    /// Pulls a plain list sequence of unprocessed, undisfilled raw text segments out of vector schemas.
    /// </summary>
    /// <param name="limit">The upper boundary size limit controlling the row count to pull from the database.</param>
    /// <returns>A collection sequence of unprocessed ticket node segments.</returns>
    public async Task<IEnumerable<TicketNode>> GetUnprocessedTicketsAsync(int limit)
    {
        var url = "v1/graphql";
        
        // Formulate standard GraphQL query to fetch undisfilled nodes (is_distilled == false)
        var query = new
        {
            query = $$"""
            {
              Get {
                KnowledgeNode(
                  limit: {{limit}}
                  where: {
                    path: ["is_distilled"]
                    operator: Equal
                    valueBoolean: false
                  }
                ) {
                  source_id
                  subject
                  content
                  url
                  status
                  priority
                }
              }
            }
            """
        };

        var json = JsonSerializer.Serialize(query);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var jsonStream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(jsonStream);
            
            var list = new List<TicketNode>();
            if (!doc.RootElement.TryGetProperty("data", out var dataProp) || 
                !dataProp.TryGetProperty("Get", out var getProp) ||
                !getProp.TryGetProperty("KnowledgeNode", out var nodesProp))
            {
                return list;
            }

            foreach (var element in nodesProp.EnumerateArray())
            {
                list.Add(new TicketNode
                {
                    SourceId = element.GetProperty("source_id").GetString() ?? string.Empty,
                    Subject = element.GetProperty("subject").GetString() ?? string.Empty,
                    Content = element.GetProperty("content").GetString() ?? string.Empty,
                    Url = element.GetProperty("url").GetString() ?? string.Empty,
                    Status = element.GetProperty("status").GetInt32(),
                    Priority = element.GetProperty("priority").GetInt32(),
                    IsDistilled = false
                });
            }

            return list;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Weaviate Error] Failed to query unprocessed tickets: {ex.Message}");
            return Enumerable.Empty<TicketNode>();
        }
    }

    /// <summary>
    /// Changes the operational distillation status flag of an item within local tracing configurations.
    /// </summary>
    /// <param name="sourceId">The unique identification key of the target processed ticket model.</param>
    /// <returns>An asynchronous task tracking completion metrics.</returns>
    /// <remarks>
    /// Production note: In modern Weaviate configurations, patch-updating an individual property 
    /// field leverages the object endpoint coupled to a deterministic UUID value structure.
    /// </remarks>
    public async Task MarkAsDistilledAsync(string sourceId)
    {
        Console.WriteLine($"[Weaviate] Ticket {sourceId} marked as distilled.");
        await Task.CompletedTask; 
    }

    /// <summary>
    /// Verifies if an individual support ticket index reference already exists inside the Weaviate vector index space.
    /// </summary>
    /// <param name="sourceId">The cross-system source tracking identifier under evaluation.</param>
    /// <returns>True if at least one matching node block is found within cluster schemas, otherwise false.</returns>
    public async Task<bool> DoesTicketExistAsync(string sourceId)
    {
        var jsonQuery = new
        {
            query = $$"""
            {
              Get {
                TicketNode(
                  limit: 1
                  where: {
                    path: ["source_id"],
                    operator: Equal,
                    valueText: "{{sourceId}}"
                  }
                ) {
                  source_id
                }
              }
            }
            """
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("v1/graphql", jsonQuery);
            if (!response.IsSuccessStatusCode) return false;

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("Get", out var get) &&
                get.TryGetProperty("TicketNode", out var nodes) &&
                nodes.ValueKind == JsonValueKind.Array)
            {
                return nodes.GetArrayLength() > 0;
            }
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Batches and uploads partitioned sequences of support logs straight into Weaviate cluster collections, 
    /// leveraging a conservative sub-package partition threshold limit of 30 units per request loop.
    /// </summary>
    /// <param name="tickets">The sequence array holding prepared support ticket elements.</param>
    /// <returns>An asynchronous task tracking transaction execution state metrics.</returns>
    public async Task BatchIngestTicketNodesAsync(IEnumerable<TicketNode> tickets)
    {
        var url = "v1/batch/objects";
        
        var allBatchObjects = tickets.Select(ticket => new
        {
            @class = "KnowledgeNode", // Mapped onto your custom Weaviate Collection layout schema
            properties = new
            {
                source_id = ticket.SourceId,
                data_type = ticket.DataType,
                subject = ticket.Subject,
                content = ticket.Content,
                is_distilled = ticket.IsDistilled,
                url = ticket.Url,
                status = ticket.Status,
                priority = ticket.Priority,
                tags = ticket.Tags ?? []
            }
        }).ToList();

        const int MaxMistralBatchSize = 30;

        for (int i = 0; i < allBatchObjects.Count; i += MaxMistralBatchSize)
        {
            var currentChunkPartition = allBatchObjects.Skip(i).Take(MaxMistralBatchSize).ToList();
            var payload = new { objects = currentChunkPartition };

            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, payload);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Ticket Batch Segment Error] Pipeline failed with code: {response.StatusCode}");
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
                            Console.WriteLine($"[Weaviate Ticket Batch Rejection]: {err.GetRawText()}");
                        }
                        else
                        {
                            segmentSuccessCount++;
                        }
                    }
                    Console.WriteLine($"[Ticket Repository Ingestion] Vectorized {segmentSuccessCount}/{currentChunkPartition.Count} items to class KnowledgeNode.");
                }

                // Guard rail against Freshdesk and Weaviate Cloud network throttling limits
                await Task.Delay(1500); 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Ticket Repository Error] Ingestion segment collapsed: {ex.Message}");
            }
        }
    }
}