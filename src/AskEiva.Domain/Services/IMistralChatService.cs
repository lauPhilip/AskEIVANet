using System.Collections.Generic;
using AskEiva.Domain.ValueObjects;
using AskEiva.Domain.Entities;

namespace AskEiva.Domain.Services;

public interface IMistralChatService
{
    IAsyncEnumerable<string> GenerateStreamingChatResponseAsync(
        string userQuestion, 
        IEnumerable<RetrievalMatch> semanticContext, 
        IEnumerable<GraphContextChain> structuralGraphMashes,
        IEnumerable<ChatTurn> conversationHistory
    );
}