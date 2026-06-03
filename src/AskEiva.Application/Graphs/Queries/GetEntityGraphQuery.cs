using System;
using System.Collections.Generic;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Services; // Points to your GraphMetricsDto location
using MediatR;

namespace AskEiva.Application.Graphs.Queries;

public record GetEntityGraphQuery : IRequest<GraphNetworkQueryResult>
{
    public string FilterText { get; init; } = string.Empty;
}

public class GraphNetworkQueryResult
{
    public List<object> Nodes { get; set; } = new();
    public List<object> Edges { get; set; } = new();
    public GraphMetricsDto Metrics { get; set; } = new();
}