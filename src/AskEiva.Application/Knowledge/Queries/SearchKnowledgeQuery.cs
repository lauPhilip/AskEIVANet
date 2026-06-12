using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AskEiva.Domain.Repositories;
using AskEiva.Domain.ValueObjects;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Services;
using MediatR;

namespace AskEiva.Application.Knowledge.Queries;

/// <summary>
/// MediatR query to search the knowledge base using a natural language question.
/// </summary>
/// <param name="UserQuestion">The plain-text search string or query entered by the user.</param>
/// <param name="MaxResults">The maximum number of reference records to return from vector storage layers.</param>
public record SearchKnowledgeQuery(string UserQuestion, int MaxResults) : IRequest<SearchQueryResult>;

/// <summary>
/// Wraps the multi-source retrieval outputs and the live chat token reply stream.
/// </summary>
/// <param name="SemanticMatches">The list of prioritized text chunks returned via semantic text search passes.</param>
/// <param name="RelevantGraphRelations">The structural graph relationships matching the conversational query context.</param>
/// <param name="SearchQuery">The verified input query string utilized to execute the system lookups.</param>
/// <param name="AnswerStream">An asynchronous stream of characters providing real-time text token responses from the AI model.</param>
public record SearchQueryResult(
    List<RetrievalMatch> SemanticMatches,
    List<GraphContextChain> RelevantGraphRelations, 
    string SearchQuery,
    IAsyncEnumerable<string> AnswerStream
);

/// <summary>
/// Orchestrates the unified search pipeline by querying semantic document chunks and graph links in parallel, 
/// and building a streamed AI token response.
/// </summary>
public class SearchKnowledgeQueryHandler : IRequestHandler<SearchKnowledgeQuery, SearchQueryResult>
{
    private readonly IKnowledgeRetrievalRepository _retrievalRepository;
    private readonly IMistralChatService _chatService;

    /// <summary>
    /// Initializes a new instance of the handler with database search providers and conversational engine interfaces.
    /// </summary>
    /// <param name="retrievalRepository">The vector database interface handling technical lookups.</param>
    /// <param name="chatService">The chat service managing model prompt generations.</param>
    public SearchKnowledgeQueryHandler(
        IKnowledgeRetrievalRepository retrievalRepository, 
        IMistralChatService chatService)
    {
        _retrievalRepository = retrievalRepository;
        _chatService = chatService;
    }

    /// <summary>
    /// Processes the input query, running concurrent lookups across diverse database indexes to build an integrated grounding context payload for the AI model.
    /// </summary>
    /// <param name="request">The search instruction containing target keywords and collection sizes.</param>
    /// <param name="cancellationToken">Token used to monitor and halt active asynchronous tasks if required.</param>
    /// <returns>A search query result wrapper object containing references, relationships, and the active token stream.</returns>
    public async Task<SearchQueryResult> Handle(SearchKnowledgeQuery request, CancellationToken cancellationToken)
    {
        // Guard clause to catch empty queries immediately before querying database layers
        if (string.IsNullOrWhiteSpace(request.UserQuestion))
        {
            return new SearchQueryResult(new(), new(), string.Empty, EmptyStream());
        }

        // Initialize lookup operations for both text chunk records and relational knowledge chains
        var semanticTask = _retrievalRepository.SearchSemanticChunksAsync(request.UserQuestion, request.MaxResults);
        var graphTask = _retrievalRepository.SearchGraphTriplesAsync(request.UserQuestion, request.MaxResults);

        // Execute both database search paths concurrently to minimize retrieval latency overheads
        await Task.WhenAll(semanticTask, graphTask);

        var semanticMatches = semanticTask.Result.ToList();
        var graphChains = graphTask.Result.ToList(); 

        // Pass the collected reference files into the AI client to start the text stream generation
        var stream = _chatService.GenerateStreamingChatResponseAsync(
            request.UserQuestion, 
            semanticMatches, 
            graphChains,
            System.Linq.Enumerable.Empty<ChatTurn>()
        );

        return new SearchQueryResult(
            SemanticMatches: semanticMatches,
            RelevantGraphRelations: graphChains,
            SearchQuery: request.UserQuestion,
            AnswerStream: stream
        );
    }

    /// <summary>
    /// Returns a predefined streaming message fallback if the search query is blank.
    /// </summary>
    /// <returns>An asynchronous stream containing error summary text.</returns>
    private async IAsyncEnumerable<string> EmptyStream()
    {
        yield return "Query cannot be blank.";
        await Task.CompletedTask;
    }
}