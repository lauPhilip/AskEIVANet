using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Repositories;
using AskEiva.Domain.Services;
using MediatR;

namespace AskEiva.Application.Graphs.Commands;

// 💡 ADDED: Progress hook instance added directly into the MediatR request wrapper
public record BuildGlobalContextGraphCommand(
    int BatchSize = 500, 
    IProgress<GraphProgressReport>? ProgressTracker = null
) : IRequest<GraphBuildResult>;

public record GraphBuildResult(int ProcessedTickets, int EdgesCreated, bool IsCompleted);

public record GraphProgressReport(int CurrentCount, int TotalCount, string CurrentTicketId, string Message);

public class BuildGlobalContextGraphCommandHandler : IRequestHandler<BuildGlobalContextGraphCommand, GraphBuildResult>
{
    private readonly IGraphRepository _graphRepository;
    private readonly IKnowledgeRetrievalRepository _retrievalRepository;
    private readonly ITicketRepository _ticketRepository; 
    private readonly IMistralDistillationService _mistralService;

    public BuildGlobalContextGraphCommandHandler(
        IGraphRepository graphRepository,
        IKnowledgeRetrievalRepository retrievalRepository,
        ITicketRepository ticketRepository, 
        IMistralDistillationService mistralService)
    {
        _graphRepository = graphRepository;
        _retrievalRepository = retrievalRepository;
        _ticketRepository = ticketRepository; 
        _mistralService = mistralService;
    }

public async Task<GraphBuildResult> Handle(BuildGlobalContextGraphCommand request, CancellationToken cancellationToken)
    {
        // 1. Fetch a distinct set of unique support ticket tracking references
        var ticketHeaders = await _ticketRepository.GetUnprocessedTicketHeadersAsync(request.BatchSize); 
        int totalPoolSize = ticketHeaders.Count();
        int itemIndexCounter = 0;
        int edgesCreatedCounter = 0;

        foreach (var header in ticketHeaders)
        {
            if (cancellationToken.IsCancellationRequested) break;

            itemIndexCounter++;
            var ticketDbId = header.SourceId ?? string.Empty;

            // IDEMPOTENCY GUARD: Fast pass check
            bool alreadyExists = await _graphRepository.HasTicketBeenProcessedAsync(ticketDbId);
            if (alreadyExists)
            {
                request.ProgressTracker?.Report(new GraphProgressReport(
                    itemIndexCounter, totalPoolSize, ticketDbId, $"Skipping {ticketDbId} (Mesh path already verified)"
                ));
                continue;
            }

            request.ProgressTracker?.Report(new GraphProgressReport(
                itemIndexCounter, totalPoolSize, ticketDbId, $"Reassembling fragmented chunks for ticket {ticketDbId}..."
            ));

            // 💡 THE ARCHITECTURAL STEP: Dynamic Parent-Child Stitching
            string fullUnifiedTicketBody = await _ticketRepository.GetStitchedTicketContentAsync(ticketDbId);
            
            if (string.IsNullOrWhiteSpace(fullUnifiedTicketBody)) continue;

            request.ProgressTracker?.Report(new GraphProgressReport(
                itemIndexCounter, totalPoolSize, ticketDbId, $"Querying vector space cross-references for {ticketDbId}..."
            ));

            // Execute nearObject proximity searches against Navipac docs using the main tracking node
            var docMatches = await _retrievalRepository.SearchTechnicalContextAsync(ticketDbId, maxResults: 3);

            var globalCandidatesContextText = "--- ARCHIVED RELEASE AND SPECIFICATION BLOCKS ---\n";
            foreach (var match in docMatches)
            {
                globalCandidatesContextText += $"[Collection: {match.CollectionName} | Node ID: {match.Id}]: {match.Content}\n";
            }

            // Send the perfectly stitched whole ticket text into the Mistral distillation engine
            var decision = await _mistralService.CompileContextChainAsync(
                ticketId: ticketDbId,
                ticketBody: fullUnifiedTicketBody, // Complete conversational stream
                candidatesContext: globalCandidatesContextText
            );

            if (!decision.IsLinked) continue;

            var graphChain = new GraphContextChain
            {
                TicketId = ticketDbId,
                TicketTitle = ticketDbId,
                MainProductContext = decision.MainProductContext,
                ScenarioType = decision.ScenarioType,
                SharedPathChain = decision.SharedPathChain,
                Predicates = decision.Predicates,
                EnvironmentalContextSummary = decision.EnvironmentalContextSummary,
                ConfidenceScore = (int)(decision.ConfidenceScore * 100)
            };

            await _graphRepository.InsertChainAsync(graphChain);
            edgesCreatedCounter++;
        }

        return new GraphBuildResult(itemIndexCounter, edgesCreatedCounter, true);
    }
}