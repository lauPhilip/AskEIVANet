using System;
using System.Threading;
using System.Threading.Tasks;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Repositories;
using MediatR;

namespace AskEiva.Application.QualityAssurance.Commands;

/// <summary>
/// Handles the storage of user feedback metrics to facilitate reinforcement learning telemetry and system tracking.
/// </summary>
public class SubmitSwipeFeedbackCommandHandler : IRequestHandler<SubmitSwipeFeedbackCommand, bool>
{
    private readonly IKnowledgeRetrievalRepository _repository;

    /// <summary>
    /// Initializes a new instance of the handler with the necessary search and logging repository interface.
    /// </summary>
    /// <param name="repository">The database repository used to persist system feedback logs.</param>
    public SubmitSwipeFeedbackCommandHandler(IKnowledgeRetrievalRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Processes incoming feedback requests, converting payload attributes into an evaluation data model and storing them securely.
    /// </summary>
    /// <param name="request">The feedback parameters specifying user approvals, queries, and manual corrections.</param>
    /// <param name="cancellationToken">Token utilized to monitor and safely interrupt background tasks.</param>
    /// <returns>True if the data model was successfully saved, otherwise false.</returns>
    public async Task<bool> Handle(SubmitSwipeFeedbackCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Map incoming command properties onto the domain telemetry log structure
            var telemetryLog = new EvaluationFeedbackLog
            {
                Query = request.Query,
                ProposedAnswer = request.GeneratedAnswer,
                IsApproved = request.IsApproved,
                CorrectionNotes = request.CorrectionNotes,
                Phase = request.TargetCollectionBias
            };

            // Commit the structural feedback model directly to the database layer
            await _repository.SaveSwipeTelemetryAsync(telemetryLog);
            return true;
        }
        catch (Exception ex)
        {
            // Capture and track operational issues safely without crashing the client request pipeline
            Console.WriteLine($"[RLHF Telemetry Logging Error]: {ex.Message}");
            return false;
        }
    }
}