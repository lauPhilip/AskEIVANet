using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Services; 
using AskEiva.Domain.Repositories; 
using MediatR;

namespace AskEiva.Application.Graphs.Queries;

/// <summary>
/// MediatR query to fetch and transform raw knowledge graph records from vector storage into a structured network format.
/// </summary>
public record GetEntityGraphQuery : IRequest<GraphNetworkQueryResult>
{
    /// <summary>
    /// Gets or initializes an optional text filter to isolate specific nodes or categories within the network graph.
    /// </summary>
    public string FilterText { get; init; } = string.Empty;
}

/// <summary>
/// Represents the complete data network payload required to render the interactive graphical canvas.
/// </summary>
public class GraphNetworkQueryResult
{
    /// <summary>
    /// Gets or sets the collection of unique entity nodes in the network.
    /// </summary>
    public List<GraphUiNode> Nodes { get; set; } = new();

    /// <summary>
    /// Gets or sets the collection of directional relationship edges connecting the nodes.
    /// </summary>
    public List<GraphUiEdge> Edges { get; set; } = new();

    /// <summary>
    /// Gets or sets the statistical metadata metrics computed for the active network topology.
    /// </summary>
    public GraphMetricsDto Metrics { get; set; } = new();
}

/// <summary>
/// Defines a single localized visual entity inside the user interface canvas network.
/// </summary>
public class GraphUiNode
{
    /// <summary>
    /// Gets or sets the unique, case-insensitive node identifier.
    /// </summary>
    [JsonPropertyName("id")] 
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable text label displayed on the canvas interface.
    /// </summary>
    [JsonPropertyName("label")] 
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category group used to color-code or group the element (e.g., ticket, product, scenario).
    /// </summary>
    [JsonPropertyName("group")] 
    public string Group { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the scale weight or size metric determining the visual radius of the node on screen.
    /// </summary>
    [JsonPropertyName("val")] 
    public int Val { get; set; }
}

/// <summary>
/// Defines a directional connector linking two separate data nodes within the user interface canvas.
/// </summary>
public class GraphUiEdge
{
    /// <summary>
    /// Gets or sets the unique tracking identifier for this specific connection.
    /// </summary>
    [JsonPropertyName("id")] 
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source node identifier matching standard D3 or vis.js graph schemas.
    /// </summary>
    [JsonPropertyName("source")] 
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target destination node identifier matching standard D3 or vis.js graph schemas.
    /// </summary>
    [JsonPropertyName("target")] 
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identical structural origin node key, utilized for canvas component path fallback rules.
    /// </summary>
    [JsonPropertyName("from")] 
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identical structural destination node key, utilized for canvas component path fallback rules.
    /// </summary>
    [JsonPropertyName("to")] 
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the relational text description rendered directly on the connection path lines.
    /// </summary>
    [JsonPropertyName("label")] 
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the relative connection thickness calculated from AI validation confidence scores.
    /// </summary>
    [JsonPropertyName("weight")] 
    public double Weight { get; set; }
}

/// <summary>
/// Processes the entity graph query by extracting raw JSON data tables from vector storage 
/// and building a fully mapped entity relation network.
/// </summary>
public class GetEntityGraphQueryHandler : IRequestHandler<GetEntityGraphQuery, GraphNetworkQueryResult>
{
    private readonly IKnowledgeRetrievalRepository _retrievalRepository;

    /// <summary>
    /// Initializes a new instance of the handler with the necessary retrieval repository interface.
    /// </summary>
    /// <param name="retrievalRepository">The search repository used to pull raw JSON context meshes.</param>
    public GetEntityGraphQueryHandler(IKnowledgeRetrievalRepository retrievalRepository)
    {
        _retrievalRepository = retrievalRepository;
    }

    /// <summary>
    /// Standardizes the raw JSON input from vector storage into a safe, strongly-typed object network for frontend layout rendering engines.
    /// </summary>
    /// <param name="request">The query containing optional filtering keywords.</param>
    /// <param name="cancellationToken">Token used to safely monitor and cancel ongoing background execution pipelines.</param>
    /// <returns>A structured object holding collections of nodes, edges, and density metrics.</returns>
    public async Task<GraphNetworkQueryResult> Handle(GetEntityGraphQuery request, CancellationToken cancellationToken)
    {
        var result = new GraphNetworkQueryResult();
        result.Metrics.PipelineStatus = "Operational";

        try
        {
            // Pull the raw topological schema graph output straight from the database provider
            JsonElement jsonRoot = await _retrievalRepository.FetchRawGraphMeshJsonAsync(request.FilterText);
            
            if (jsonRoot.ValueKind == JsonValueKind.Undefined || !jsonRoot.TryGetProperty("data", out var dataNode)) 
                return result;

            var root = dataNode.GetProperty("Get");

            if (root.TryGetProperty("GraphContextChain", out var chainArray) && chainArray.ValueKind == JsonValueKind.Array)
            {
                var registeredNodes = new HashSet<string>();
                int edgeIdCounter = 1;

                result.Metrics.TotalTriples = chainArray.GetArrayLength();

                foreach (var item in chainArray.EnumerateArray())
                {
                    string rawTicketId = (item.GetProperty("ticket_id").GetString() ?? "Unknown").Trim();
                    string rawProduct = (item.GetProperty("main_product_context").GetString() ?? "UnknownProduct").Trim();
                    string rawScenario = (item.GetProperty("scenario_type").GetString() ?? "GeneralSupport").Trim();
                    int confidence = item.GetProperty("confidence_score").GetInt32();

                    // Create normalized, uppercase string keys to guarantee node uniqueness across connections
                    string ticketKey = rawTicketId.ToUpperInvariant();
                    string productKey = rawProduct.ToUpperInvariant();
                    string scenarioKey = rawScenario.ToUpperInvariant();

                    // 1. Build unique nodes and assign relative visual dimensions based on entity groupings
                    if (registeredNodes.Add(ticketKey))
                    {
                        result.Nodes.Add(new GraphUiNode { Id = ticketKey, Label = rawTicketId, Group = "ticket", Val = 20 });
                    }
                    
                    if (registeredNodes.Add(productKey))
                    {
                        result.Nodes.Add(new GraphUiNode { Id = productKey, Label = rawProduct, Group = "product", Val = 35 });
                    }
                    
                    if (registeredNodes.Add(scenarioKey))
                    {
                        result.Nodes.Add(new GraphUiNode { Id = scenarioKey, Label = rawScenario, Group = "scenario", Val = 25 });
                    }

                    // 2. Extract relationship labels, falling back to basic link tags if empty
                    string edgePredicate = "RESOLVES_WITH";
                    if (item.TryGetProperty("predicates", out var predProp) && predProp.ValueKind == JsonValueKind.Array && predProp.GetArrayLength() > 0)
                    {
                        edgePredicate = predProp.EnumerateArray().First().GetString() ?? "LINKS_TO";
                    }

                    string edgeId1 = $"e_{edgeIdCounter++}";
                    string edgeId2 = $"e_{edgeIdCounter++}";

                    // 3. Construct directional connection structures linking Ticket -> Product and Product -> Scenario
                    result.Edges.Add(new GraphUiEdge 
                    { 
                        Id = edgeId1, 
                        Source = ticketKey, From = ticketKey, 
                        Target = productKey, To = productKey, 
                        Label = edgePredicate, 
                        Weight = (double)confidence / 100 
                    });
                    
                    result.Edges.Add(new GraphUiEdge 
                    { 
                        Id = edgeId2, 
                        Source = productKey, From = productKey, 
                        Target = scenarioKey, To = scenarioKey,   
                        Label = "CLASSIFIED_AS", 
                        Weight = 0.8 
                    });
                }

                // 4. Calculate network topology metrics for dashboard analytics panels
                result.Metrics.TotalNodes = registeredNodes.Count;
                result.Metrics.ConnectedClusters = result.Nodes.Count(n => n.Group == "scenario");
                
                result.Metrics.SemanticDensity = result.Nodes.Count > 1 
                    ? Math.Round((double)result.Edges.Count / (result.Nodes.Count * (result.Nodes.Count - 1) / 2.0), 3) 
                    : 0.0;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Graph Telemetry Data Compilation Error]: {ex.Message}");
        }

        return result;
    }
}