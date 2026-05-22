using System.Collections.Generic;
using System.Threading.Tasks;
using AskEiva.Domain.ValueObjects;
using AskEiva.Domain.Entities;

namespace AskEiva.Domain.Repositories;

public interface IKnowledgeRetrievalRepository
{
    Task<IEnumerable<RetrievalMatch>> SearchSemanticChunksAsync(string userQuery, int limit);
    Task<IEnumerable<KnowledgeTriple>> SearchGraphTriplesAsync(string userQuery, int limit);
    Task<int> GetTotalClassCountAsync(string className);
    Task<int> GetDistinctSourceCountAsync(string className, string groupProperty);
    Task<System.Text.Json.JsonElement> GetRawInteractionLogsAsync(int limit);
    Task LogInteractionAsync(string query, string answer, bool wasSuccessful);
    Task BatchIngestReleaseNodesAsync(IEnumerable<SoftwareReleaseNode> nodes);
}