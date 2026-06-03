using System.Collections.Generic;
using System.Threading.Tasks;
using AskEiva.Domain.ValueObjects;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Services;

namespace AskEiva.Domain.Repositories;

public interface IKnowledgeRetrievalRepository
{
    Task<IEnumerable<RetrievalMatch>> SearchSemanticChunksAsync(string userQuery, int limit);
    Task<IEnumerable<GraphContextChain>> SearchGraphTriplesAsync(string userQuery, int limit);
    Task<int> GetTotalClassCountAsync(string className);
    Task<int> GetDistinctSourceCountAsync(string className, string groupProperty);
    Task<System.Text.Json.JsonElement> GetRawInteractionLogsAsync(int limit);
    Task LogInteractionAsync(string query, string answer, bool wasSuccessful);
    Task BatchIngestReleaseNodesAsync(IEnumerable<SoftwareReleaseNode> nodes);
    Task<bool> DoesProductVersionExistAsync(string product, string version);
    Task<List<TechnicalContextSearchResult>> SearchTechnicalContextAsync(string ticketId, int maxResults);
}

public class TechnicalContextSearchResult
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty; // "DocumentLibrary" or "SoftwareReleaseNode"
    public string SuggestedEdgeType { get; set; } = "RESOLVED_BY";
}