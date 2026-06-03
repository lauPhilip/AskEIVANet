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

// 💡 RECORD TYPING UPDATE
public record SearchKnowledgeQuery(string UserQuestion, int MaxResults) : IRequest<SearchQueryResult>;

public record SearchQueryResult(
    List<RetrievalMatch> SemanticMatches,
    List<GraphContextChain> RelevantGraphRelations, 
    string SearchQuery,
    IAsyncEnumerable<string> AnswerStream
);

public class SearchKnowledgeQueryHandler : IRequestHandler<SearchKnowledgeQuery, SearchQueryResult>
{
    private readonly IKnowledgeRetrievalRepository _retrievalRepository;
    private readonly IMistralChatService _chatService;

    public SearchKnowledgeQueryHandler(
        IKnowledgeRetrievalRepository retrievalRepository, 
        IMistralChatService chatService)
    {
        _retrievalRepository = retrievalRepository;
        _chatService = chatService;
    }

    public async Task<SearchQueryResult> Handle(SearchKnowledgeQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserQuestion))
        {
            return new SearchQueryResult(new(), new(), string.Empty, EmptyStream());
        }

        var semanticTask = _retrievalRepository.SearchSemanticChunksAsync(request.UserQuestion, request.MaxResults);
        var graphTask = _retrievalRepository.SearchGraphTriplesAsync(request.UserQuestion, request.MaxResults);

        await Task.WhenAll(semanticTask, graphTask);

        var semanticMatches = semanticTask.Result.ToList();
        var graphChains = graphTask.Result.ToList(); // 💡 Stored as native GraphContextChain sequences

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

    private async IAsyncEnumerable<string> EmptyStream()
    {
        yield return "Query cannot be blank.";
        await Task.CompletedTask;
    }
}