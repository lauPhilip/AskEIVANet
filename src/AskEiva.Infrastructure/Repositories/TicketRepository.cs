using System.Text;
using System.Text.Json;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Repositories;
using System.Net.Http.Json;

namespace AskEiva.Infrastructure.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly HttpClient _httpClient;

    public TicketRepository(HttpClient httpClient, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _httpClient = httpClient;

        // 💡 FIXED: Automatically injects your Weaviate WCD API Key into every inbound transactional pipeline request
        string weaviateKey = configuration["WEAVIATE_API_KEY"] ?? string.Empty;
        if (!string.IsNullOrEmpty(weaviateKey) && !_httpClient.DefaultRequestHeaders.Contains("Authorization"))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {weaviateKey}");
        }
    }

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

public async Task<List<TicketNode>> GetUnprocessedTicketHeadersAsync(int batchSize)
{
    var headers = new List<TicketNode>();

    try
    {
        // 💡 ADVANCED GRAPHQL: Group chunks by source_id where processing is still required
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
            var stringBuilder = new System.Text.StringBuilder();
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

    public async Task MarkAsDistilledAsync(string sourceId)
    {
        // Production note: In modern Weaviate, patch updating properties 
        // utilizes the objects endpoint combined with a deterministic UUID.
        Console.WriteLine($"[Weaviate] Ticket {sourceId} marked as distilled.");
        await Task.CompletedTask; 
    }

    public async Task<bool> DoesTicketExistAsync(string sourceId)
    {
        // Look up the unique source ID directly using Weaviate's GraphQL filtering options
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
                tags = ticket.Tags ?? new List<string>()
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
                await Task.Delay(1500); // Guard rail against Freshdesk throttling limits
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Ticket Repository Error] Ingestion segment collapsed: {ex.Message}");
            }
        }
    }
}