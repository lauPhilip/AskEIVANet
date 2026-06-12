using System.Collections.Generic;
using AskEiva.Domain.Entities;
using AskEiva.Domain.ValueObjects;

namespace AskEiva.Domain.Services;

/// <summary>
/// Defines the low-level inference contract responsible for interacting directly with Mistral AI platform endpoints 
/// to stream back real-time, token-by-token conversational text strings.
/// </summary>
public interface IMistralChatService
{
    /// <summary>
    /// Constructs a fully contextualized interaction prompt payload and opens a real-time streaming channel 
    /// with the Mistral model engine to yield answers sequentially.
    /// </summary>
    /// <param name="userQuestion">The active, incoming natural language query or troubleshooting request from the user.</param>
    /// <param name="semanticContext">The collection of text segments pulled from technical manuals by vector similarity lookups.</param>
    /// <param name="structuralGraphMashes">The collection of distilled relationship paths linking cross-system contexts together.</param>
    /// <param name="conversationHistory">The back-and-forth log of previous messages exchanged within the active session to preserve chat context.</param>
    /// <returns>An asynchronous text stream yielding individual words or token fragments as the language model calculates them.</returns>
    IAsyncEnumerable<string> GenerateStreamingChatResponseAsync(
        string userQuestion, 
        IEnumerable<RetrievalMatch> semanticContext, 
        IEnumerable<GraphContextChain> structuralGraphMashes,
        IEnumerable<ChatTurn> conversationHistory
    );
}