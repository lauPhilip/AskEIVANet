using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AskEiva.Domain.Repositories;
using MediatR;

namespace AskEiva.Application.Telemetry.Queries;

public record GetDashboardTelemetryQuery : IRequest<TelemetryDashboardResult>;

// 💡 FIXED: Included TotalSoftwareReleaseChunks right inside the MediatR communication DTO contract!
public record TelemetryDashboardResult(
    int TotalUniqueDocuments,
    int TotalUniqueTickets,
    int TotalSoftwareReleases,
    int TotalSoftwareReleaseChunks, 
    int TotalGraphContextChains,
    List<InteractionLogItemDto> Logs
);

public record InteractionLogItemDto(
    DateTime Timestamp, 
    string UserPrompt, 
    string CopilotResponse, 
    string ModelUsed,
    string FeedbackState
);

public class GetDashboardTelemetryQueryHandler : IRequestHandler<GetDashboardTelemetryQuery, TelemetryDashboardResult>
{
    private readonly IKnowledgeRetrievalRepository _retrievalRepository;

    public GetDashboardTelemetryQueryHandler(IKnowledgeRetrievalRepository retrievalRepository)
    {
        _retrievalRepository = retrievalRepository;
    }

    public async Task<TelemetryDashboardResult> Handle(GetDashboardTelemetryQuery request, CancellationToken cancellationToken)
    {
        // Parent distinct trackers
        var docsTask = _retrievalRepository.GetDistinctSourceCountAsync("DocumentationLibrary", "document_id");
        var ticketsTask = _retrievalRepository.GetDistinctSourceCountAsync("KnowledgeNode", "source_id"); 
        var releaseTask = _retrievalRepository.GetDistinctSourceCountAsync("SoftwareReleaseNode", "full_version_title"); 
        var graphChainsTask = _retrievalRepository.GetDistinctSourceCountAsync("GraphContextChain","ticket_id");
        var releaseChunksTask = _retrievalRepository.GetTotalClassCountAsync("SoftwareReleaseNode");
        
        var logsTask = _retrievalRepository.GetRawInteractionLogsAsync(limit: 500);

        // Parallel task processing pass
        await Task.WhenAll(docsTask, ticketsTask, releaseTask, releaseChunksTask);

        var logsResult = await logsTask;
        var activeLogs = new List<InteractionLogItemDto>();

        if (logsResult.ValueKind != JsonValueKind.Undefined &&
            logsResult.TryGetProperty("data", out var data) &&
            data.TryGetProperty("Get", out var get) &&
            get.TryGetProperty("InteractionLog", out var logArray) &&
            logArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in logArray.EnumerateArray())
            {
                string queryText = item.TryGetProperty("query", out var q) ? q.GetString() ?? "" : "";
                string answerText = item.TryGetProperty("answer", out var a) ? a.GetString() ?? "" : "";
                
                bool successful = item.TryGetProperty("was_successful", out var ws) && ws.GetBoolean();
                string feedbackLabel = successful ? "👍 Success" : "❌ Issue / Conflict";

                DateTime logTime = DateTime.UtcNow;
                if (item.TryGetProperty("timestamp", out var ts) && DateTime.TryParse(ts.GetString(), out var parsedTime))
                {
                    logTime = parsedTime;
                }

                activeLogs.Add(new InteractionLogItemDto(
                    Timestamp: logTime,
                    UserPrompt: queryText,
                    CopilotResponse: answerText,
                    ModelUsed: "mistral-large-latest",
                    FeedbackState: feedbackLabel
                ));
            }
        }

        // Return the fully computed counts mapped to contract indices
        return new TelemetryDashboardResult(
            TotalUniqueDocuments: docsTask.Result,
            TotalUniqueTickets: ticketsTask.Result, 
            TotalSoftwareReleases: releaseTask.Result,
            TotalSoftwareReleaseChunks: releaseChunksTask.Result,
            TotalGraphContextChains: graphChainsTask.Result,
            Logs: activeLogs
        );
    }
}