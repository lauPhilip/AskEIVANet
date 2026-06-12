using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Repositories;

namespace AskEiva.Infrastructure.Repositories;

/// <summary>
/// Implements the <see cref="IGraphRepository"/> contract using a raw HTTP GraphQL pipeline 
/// to query, insert, and manage knowledge graph relationship paths directly inside a Weaviate cluster instance.
/// </summary>
public class GraphRepository : IGraphRepository
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphRepository"/> class with a pre-configured HTTP client factory.
    /// </summary>
    /// <param name="httpClient">An unmanaged or factory-managed HTTP client configured for Weaviate endpoint routing targets.</param>
    public GraphRepository(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Commits a newly distilled, relational knowledge graph context chain entity into the Weaviate vector database.
    /// </summary>
    /// <param name="chain">The structural graph context chain domain model tracking semantic predicates and confidence scores.</param>
    /// <returns>True if the payload successfully committed without server anomalies, otherwise false.</returns>
    public async Task<bool> InsertChainAsync(GraphContextChain chain)
    {
        try
        {
            // Force serializer options to use lowercase snake_case property profiles matching the database schema
            var serializationOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = false
            };

            // Package into the explicit JSON payload format that Weaviate's REST engine expects
            var payload = new
            {
                @class = "GraphContextChain",
                properties = new
                {
                    ticket_id = chain.TicketId,
                    ticket_title = chain.TicketTitle,
                    main_product_context = chain.MainProductContext,
                    scenario_type = chain.ScenarioType,
                    shared_path_chain = chain.SharedPathChain,
                    predicates = chain.Predicates,
                    environmental_context_summary = chain.EnvironmentalContextSummary,
                    confidence_score = chain.ConfidenceScore
                }
            };

            var response = await _httpClient.PostAsJsonAsync("v1/objects", payload, serializationOptions);
            
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            string errorDetails = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[Weaviate Graph Save Anomaly]: {response.StatusCode} - {errorDetails}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Graph Repository Fault]: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Queries the vector database using raw GraphQL conditional filters to check if a support ticket has already been distilled.
    /// </summary>
    /// <param name="ticketId">The unique tracking cross-system ticket identifier (e.g., "FD-10293").</param>
    /// <returns>True if the matching row count is greater than zero, ensuring processing idempotency.</returns>
    public async Task<bool> HasTicketBeenProcessedAsync(string ticketId)
    {
        try
        {
            var graphQlQuery = $$"""
            {
              Get {
                GraphContextChain(
                  where: {
                    path: ["ticket_id"]
                    operator: Equal
                    valueText: "{{ticketId}}"
                  }
                  limit: 1
                ) {
                  ticket_id
                }
              }
            }
            """;

            var response = await _httpClient.PostAsJsonAsync("v1/graphql", new { query = graphQlQuery });
            if (!response.IsSuccessStatusCode) return false;

            using var jsonDocument = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var root = jsonDocument.RootElement;

            if (root.TryGetProperty("data", out var dataNode) && 
                dataNode.TryGetProperty("Get", out var getCollection) &&
                getCollection.TryGetProperty("GraphContextChain", out var chainArray))
            {
                return chainArray.ValueKind == JsonValueKind.Array && chainArray.GetArrayLength() > 0;
            }

            return false;
        }
        catch
        {
            // Fallback defensively to process if validation verification checks drop
            return false; 
        }
    }

    /// <summary>
    /// Executes a hybrid nearVector proximity query using high-dimensional array structures 
    /// to retrieve semantic paths matching active user technical prompts.
    /// </summary>
    /// <param name="queryVector">The unalterable memory-mapped block holding the raw floating-point token calculations.</param>
    /// <param name="maxResults">The maximum priority limit count of structural paths to extract from the cluster layer.</param>
    /// <returns>A list containing matching context chains populated with confidence indicators and semantic metadata summaries.</returns>
    public async Task<List<GraphContextChain>> SearchGraphContextAsync(ReadOnlyMemory<float> queryVector, int maxResults)
    {
        var results = new List<GraphContextChain>();
        try
        {
            var vectorString = string.Join(",", queryVector.ToArray());
            var graphQlQuery = $$"""
            {
              Get {
                GraphContextChain(
                  nearVector: { vector: [{{vectorString}}] }
                  limit: {{maxResults}}
                ) {
                  ticket_id
                  ticket_title
                  main_product_context
                  scenario_type
                  shared_path_chain
                  environmental_context_summary
                  confidence_score
                }
              }
            }
            """;

            var response = await _httpClient.PostAsJsonAsync("v1/graphql", new { query = graphQlQuery });
            if (!response.IsSuccessStatusCode) return results;

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            if (doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("Get", out var get))
            {
                if (get.TryGetProperty("GraphContextChain", out var array) && array.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in array.EnumerateArray())
                    {
                        results.Add(new GraphContextChain
                        {
                            TicketId = item.GetProperty("ticket_id").GetString() ?? string.Empty,
                            TicketTitle = item.GetProperty("ticket_title").GetString() ?? string.Empty,
                            MainProductContext = item.GetProperty("main_product_context").GetString() ?? string.Empty,
                            ScenarioType = item.GetProperty("scenario_type").GetString() ?? string.Empty,
                            SharedPathChain = item.GetProperty("shared_path_chain").GetString() ?? string.Empty,
                            EnvironmentalContextSummary = item.GetProperty("environmental_context_summary").GetString() ?? string.Empty,
                            ConfidenceScore = item.GetProperty("confidence_score").GetDouble()
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GraphRAG Search Failed] Direct stream dropped: {ex.Message}");
        }
        return results;
    }

    /// <summary>
    /// Extracts all structural relationship nodes mapped out and assigned to a single cross-system support ticket identifier.
    /// </summary>
    /// <param name="ticketId">The unique identification key of the target support ticket anchor.</param>
    /// <returns>A list of matching graph context paths linked directly back to the specified ticket identifier.</returns>
    public async Task<List<GraphContextChain>> GetChainsByTicketAsync(string ticketId)
    {
        var results = new List<GraphContextChain>();
        try
        {
            var graphQlQuery = $$"""
            {
              Get {
                GraphContextChain(
                  where: { path: ["ticket_id"], operator: Equal, valueText: "{{ticketId}}" }
                ) {
                  shared_path_chain
                  environmental_context_summary
                }
              }
            }
            """;

            var response = await _httpClient.PostAsJsonAsync("v1/graphql", new { query = graphQlQuery });
            if (!response.IsSuccessStatusCode) return results;

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            if (doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("Get", out var get))
            {
                if (get.TryGetProperty("GraphContextChain", out var array) && array.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in array.EnumerateArray())
                    {
                        results.Add(new GraphContextChain
                        {
                            TicketId = ticketId,
                            SharedPathChain = item.GetProperty("shared_path_chain").GetString() ?? string.Empty,
                            EnvironmentalContextSummary = item.GetProperty("environmental_context_summary").GetString() ?? string.Empty
                        });
                    }
                }
            }
        }
        catch 
        { 
            /* Fallback defensively to return empty arrays if structural network streams fault */ 
        }
        return results;
    }
}