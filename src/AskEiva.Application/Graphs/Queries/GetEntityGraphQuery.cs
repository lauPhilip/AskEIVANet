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

// 1. Core Request Definition
public record GetEntityGraphQuery : IRequest<GraphNetworkQueryResult>
{
    public string FilterText { get; init; } = string.Empty;
}

// 2. Data Transfer Response Payload Wrapper
public class GraphNetworkQueryResult
{
    public List<GraphUiNode> Nodes { get; set; } = new();
    public List<GraphUiEdge> Edges { get; set; } = new();
    public GraphMetricsDto Metrics { get; set; } = new();
}

// 💡 STRONGLY TYPED CLASSES WITH EXPLICIT JSON TARGETING
public class GraphUiNode
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("group")] public string Group { get; set; } = string.Empty;
    [JsonPropertyName("val")] public int Val { get; set; }
}

public class GraphUiEdge
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    
    // Standard D3 targeting keys
    [JsonPropertyName("source")] public string Source { get; set; } = string.Empty;
    [JsonPropertyName("target")] public string Target { get; set; } = string.Empty;
    
    [JsonPropertyName("from")] public string From { get; set; } = string.Empty;
    [JsonPropertyName("to")] public string To { get; set; } = string.Empty;
    
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("weight")] public double Weight { get; set; }
}

// 3. THE EXECUTING HANDLER ENGINE
public class GetEntityGraphQueryHandler : IRequestHandler<GetEntityGraphQuery, GraphNetworkQueryResult>
{
    private readonly IKnowledgeRetrievalRepository _retrievalRepository;

    public GetEntityGraphQueryHandler(IKnowledgeRetrievalRepository retrievalRepository)
    {
        _retrievalRepository = retrievalRepository;
    }

    public async Task<GraphNetworkQueryResult> Handle(GetEntityGraphQuery request, CancellationToken cancellationToken)
    {
        var result = new GraphNetworkQueryResult();
        result.Metrics.PipelineStatus = "Operational";

        try
        {
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

                    // Create strict, normalized, case-insensitive ID keys for graph connectivity stability
                    string ticketKey = rawTicketId.ToUpperInvariant();
                    string productKey = rawProduct.ToUpperInvariant();
                    string scenarioKey = rawScenario.ToUpperInvariant();

                    // 1. Map Unique Nodes cleanly
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

                    // 2. Extract edge labels
                    string edgePredicate = "RESOLVES_WITH";
                    if (item.TryGetProperty("predicates", out var predProp) && predProp.ValueKind == JsonValueKind.Array && predProp.GetArrayLength() > 0)
                    {
                        edgePredicate = predProp.EnumerateArray().First().GetString() ?? "LINKS_TO";
                    }

                    string edgeId1 = $"e_{edgeIdCounter++}";
                    string edgeId2 = $"e_{edgeIdCounter++}";

                    // 3. Map Edge relationships with multi-key configuration redundancy
                    result.Edges.Add(new GraphUiEdge 
                    { 
                        Id = edgeId1, 
                        Source = ticketKey, From = ticketKey, // Ticket anchor point
                        Target = productKey, To = productKey, // Product anchor point
                        Label = edgePredicate, 
                        Weight = (double)confidence / 100 
                    });
                    
                    result.Edges.Add(new GraphUiEdge 
                    { 
                        Id = edgeId2, 
                        Source = productKey, From = productKey, // Product anchor point
                        Target = scenarioKey, To = scenarioKey,   // Scenario class archetype root
                        Label = "CLASSIFIED_AS", 
                        Weight = 0.8 
                    });
                }

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