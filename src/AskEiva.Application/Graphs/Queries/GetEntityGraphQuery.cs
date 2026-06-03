using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http.Headers; 
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
    public List<object> Nodes { get; set; } = new();
    public List<object> Edges { get; set; } = new();
    public GraphMetricsDto Metrics { get; set; } = new();
}

// 3. THE EXECUTING HANDLER ENGINE
// 3. THE EXECUTING HANDLER ENGINE
public class GetEntityGraphQueryHandler : IRequestHandler<GetEntityGraphQuery, GraphNetworkQueryResult>
{
    // 💡 DECOUPLED: Swap out HttpClient for your domain abstraction
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
            // 💡 Pull clean, authorized payload data directly from your repository channel pass
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
                    string ticketId = item.GetProperty("ticket_id").GetString() ?? "Unknown";
                    string product = item.GetProperty("main_product_context").GetString() ?? "Unknown";
                    string scenario = item.GetProperty("scenario_type").GetString() ?? "Unknown";
                    int confidence = item.GetProperty("confidence_score").GetInt32();

                    if (registeredNodes.Add(ticketId))
                        result.Nodes.Add(new { id = ticketId, label = ticketId, group = "ticket", val = 20 });
                    
                    if (registeredNodes.Add(product))
                        result.Nodes.Add(new { id = product, label = product, group = "product", val = 35 });
                    
                    if (registeredNodes.Add(scenario))
                        result.Nodes.Add(new { id = scenario, label = scenario, group = "scenario", val = 25 });

                    string edgePredicate = "RESOLVES_WITH";
                    if (item.TryGetProperty("predicates", out var predProp) && predProp.ValueKind == JsonValueKind.Array && predProp.GetArrayLength() > 0)
                    {
                        edgePredicate = predProp.EnumerateArray().First().GetString() ?? "LINKS_TO";
                    }

                    result.Edges.Add(new { id = $"e_{edgeIdCounter++}", source = ticketId, target = product, label = edgePredicate, weight = (double)confidence / 100 });
                    result.Edges.Add(new { id = $"e_{edgeIdCounter++}", source = product, target = scenario, label = "CLASSIFIED_AS", weight = 0.8 });
                }

                result.Metrics.TotalNodes = registeredNodes.Count;
                result.Metrics.ConnectedClusters = result.Nodes.Count(n => GetGroupValue(n) == "scenario");
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

    private string GetGroupValue(object node)
    {
        return node?.GetType().GetProperty("group")?.GetValue(node, null)?.ToString() ?? string.Empty;
    }
}