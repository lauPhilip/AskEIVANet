using System.Collections.Generic;
using System.Threading.Tasks;
using AskEiva.Domain.ValueObjects;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Services;
using System.Text.Json;
using System.Threading.Tasks;

namespace AskEiva.Domain.Repositories;


public enum EvaluationPhase
{
    ClosedBaseline, // Historical verified resolutions
    LiveOpenAssist  // Fresh triage items
}

public record EvaluationTestCase(string Query, List<string> ExpectedContextKeys, string GroundTruthAnswer);


public interface IKnowledgeRetrievalRepository
{
    Task<IEnumerable<RetrievalMatch>> SearchSemanticChunksAsync(string userQuery, int limit);
    Task<IEnumerable<GraphContextChain>> SearchGraphTriplesAsync(string userQuery, int limit);
    Task<JsonElement> FetchRawGraphMeshJsonAsync(string filterText);
    Task<int> GetTotalClassCountAsync(string className);
    Task<int> GetDistinctSourceCountAsync(string className, string groupProperty);
    Task<System.Text.Json.JsonElement> GetRawInteractionLogsAsync(int limit);
    Task LogInteractionAsync(string query, string answer, bool wasSuccessful);
    Task BatchIngestReleaseNodesAsync(IEnumerable<SoftwareReleaseNode> nodes);
    Task<bool> DoesProductVersionExistAsync(string product, string version);
    Task<List<TechnicalContextSearchResult>> SearchTechnicalContextAsync(string ticketId, int maxResults);
    Task<List<EvaluationTestCase>> FetchEvaluationDeckByPhaseAsync(EvaluationPhase phase, int count);
    Task SaveSwipeTelemetryAsync(EvaluationFeedbackLog log);
}

public class TechnicalContextSearchResult
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty; // "DocumentLibrary" or "SoftwareReleaseNode"
    public string SuggestedEdgeType { get; set; } = "RESOLVED_BY";
}