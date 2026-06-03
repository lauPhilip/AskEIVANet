using System.Threading.Tasks;
using System.Collections.Generic;
using AskEiva.Domain.ValueObjects;
using AskEiva.Domain.Entities;

namespace AskEiva.Domain.Services;

public interface ILlmOrchestrationService
{
    Task<string> DistillContextIntoAnswerAsync(
        string userQuestion, 
        IEnumerable<RetrievalMatch> semanticContext, 
        IEnumerable<GraphContextChain> structuralGraphMashes
    );
}