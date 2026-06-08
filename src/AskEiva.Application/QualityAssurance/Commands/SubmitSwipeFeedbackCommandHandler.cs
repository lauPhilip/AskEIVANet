using System;
using System.Threading;
using System.Threading.Tasks;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Repositories;
using MediatR;

namespace AskEiva.Application.QualityAssurance.Commands;

public class SubmitSwipeFeedbackCommandHandler : IRequestHandler<SubmitSwipeFeedbackCommand, bool>
{
    private readonly IKnowledgeRetrievalRepository _repository;

    public SubmitSwipeFeedbackCommandHandler(IKnowledgeRetrievalRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(SubmitSwipeFeedbackCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var telemetryLog = new EvaluationFeedbackLog
            {
                Query = request.Query,
                ProposedAnswer = request.GeneratedAnswer,
                IsApproved = request.IsApproved,
                CorrectionNotes = request.CorrectionNotes,
                Phase = request.TargetCollectionBias
            };

            // Commit directly to the reinforcement tracking tables
            await _repository.SaveSwipeTelemetryAsync(telemetryLog);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RLHF Telemetry Logging Error]: {ex.Message}");
            return false;
        }
    }
}