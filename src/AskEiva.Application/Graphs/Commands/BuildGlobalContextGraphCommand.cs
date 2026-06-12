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

/// <summary>
/// MediatR command to build the global knowledge context graph by processing unlinked support tickets.
/// </summary>
/// <param name="BatchSize">The maximum number of tickets to process during this command execution.</param>
/// <param name="ProgressTracker">An optional progress reporter used to send live status updates back to the UI layer.</param>
public record BuildGlobalContextGraphCommand(
    int BatchSize = 500, 
    IProgress<GraphProgressReport>? ProgressTracker = null
) : IRequest<GraphBuildResult>;

/// <summary>
/// Represents the final processing statistics after a graph compilation run.
/// </summary>
/// <param name="ProcessedTickets">The total number of support tickets checked during the run.</param>
/// <param name="EdgesCreated">The number of new relational knowledge links successfully added to the database.</param>
/// <param name="IsCompleted">True if the process completed its iteration loop successfully.</param>
public record GraphBuildResult(int ProcessedTickets, int EdgesCreated, bool IsCompleted);

/// <summary>
/// Holds real-time progress update metrics sent out during long-running background graph builds.
/// </summary>
/// <param name="CurrentCount">The relative index number of the item currently under evaluation.</param>
/// <param name="TotalCount">The total size of the active processing batch pool.</param>
/// <param name="CurrentTicketId">The unique identifier of the support ticket being handled.</param>
/// <param name="Message">A plain-text description of the current action or processing step.</param>
public record GraphProgressReport(int CurrentCount, int TotalCount, string CurrentTicketId, string Message);

/// <summary>
/// Coordinates the end-to-end graph construction process by loading raw tickets, querying vector context, 
/// distilling connections with AI, and saving cross-references into the database.
/// </summary>
public class BuildGlobalContextGraphCommandHandler : IRequestHandler<BuildGlobalContextGraphCommand, GraphBuildResult>
{
    private readonly IGraphRepository _graphRepository;
    private readonly IKnowledgeRetrievalRepository _retrievalRepository;
    private readonly ITicketRepository _ticketRepository; 
    private readonly IMistralDistillationService _mistralService;

    /// <summary>
    /// Initializes a new instance of the handler with database, search, and AI client abstractions.
    /// </summary>
    /// <param name="graphRepository">Storage repository for saving structural graph links.</param>
    /// <param name="retrievalRepository">Search engine interface for discovering technical context.</param>
    /// <param name="ticketRepository">Data repository for loading support tickets.</param>
    /// <param name="mistralService">AI service client for processing and distilling relationships.</param>
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

    /// <summary>
    /// Handles the graph build request by identifying unlinked records, gathering relevant knowledge data, 
    /// evaluating connections via the AI distillation model, and storing valid relationships.
    /// </summary>
    /// <param name="request">The structural configuration parameters for the execution run.</param>
    /// <param name="cancellationToken">Token used to gracefully halt operation execution if requested.</param>
    /// <returns>A summary data object outlining processing totals and link creation numbers.</returns>
    public async Task<GraphBuildResult> Handle(BuildGlobalContextGraphCommand request, CancellationToken cancellationToken)
    {
        // Fetch a defined subset of unique support tickets requiring relationship mapping
        var ticketHeaders = await _ticketRepository.GetUnprocessedTicketHeadersAsync(request.BatchSize); 
        int totalPoolSize = ticketHeaders.Count();
        int itemIndexCounter = 0;
        int edgesCreatedCounter = 0;

        foreach (var header in ticketHeaders)
        {
            if (cancellationToken.IsCancellationRequested) break;

            itemIndexCounter++;
            var ticketDbId = header.SourceId ?? string.Empty;

            // Verification check to make sure this ticket has not been processed during a prior run
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

            // Assemble all parent records and conversational replies into a single continuous block of text
            string fullUnifiedTicketBody = await _ticketRepository.GetStitchedTicketContentAsync(ticketDbId);
            
            if (string.IsNullOrWhiteSpace(fullUnifiedTicketBody)) continue;

            request.ProgressTracker?.Report(new GraphProgressReport(
                itemIndexCounter, totalPoolSize, ticketDbId, $"Querying vector space cross-references for {ticketDbId}..."
            ));

            // Execute vector proximity searches to find matching documentation for the ticket context
            var docMatches = await _retrievalRepository.SearchTechnicalContextAsync(ticketDbId, maxResults: 3);

            var globalCandidatesContextText = "--- ARCHIVED RELEASE AND SPECIFICATION BLOCKS ---\n";
            foreach (var match in docMatches)
            {
                globalCandidatesContextText += $"[Collection: {match.CollectionName} | Node ID: {match.Id}]: {match.Content}\n";
            }

            // Request the AI distillation service to determine if a structural relationship exists
            var decision = await _mistralService.CompileContextChainAsync(
                ticketId: ticketDbId,
                ticketBody: fullUnifiedTicketBody, 
                candidatesContext: globalCandidatesContextText
            );

            if (!decision.IsLinked) continue;

            // Structure a new graph relationship data entity based on the AI evaluation model output
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