using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using AskEiva.Domain.ValueObjects;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Services;

namespace AskEiva.Domain.Repositories;

/// <summary>
/// Specifies the testing or deployment phase profile utilized to segment quality assurance evaluation datasets.
/// </summary>
public enum EvaluationPhase
{
    /// <summary>
    /// Represents historical closed tickets with verified resolution steps used as a testing baseline.
    /// </summary>
    ClosedBaseline,

    /// <summary>
    /// Represents ongoing, fresh triage support tickets undergoing active diagnostic assistance.
    /// </summary>
    LiveOpenAssist
}

/// <summary>
/// Represents an unalterable validation test scenario case used to verify retrieval and generation quality.
/// </summary>
/// <param name="Id">The unique identification key tracking the evaluation row.</param>
/// <param name="Query">The diagnostic search string or prompt submitted during evaluation.</param>
/// <param name="ProposedAnswer">The actual conversational response text produced by the model during testing.</param>
/// <param name="GroundTruth">The verified reference solution text used to score the model response.</param>
/// <param name="ExpectedContextKeys">The collection of key identifiers that the vector search engine ought to pull.</param>
/// <param name="ContextDocumentationChunks">The actual grounding text blocks utilized by the generation pipeline during testing.</param>
public record EvaluationTestCase(
    string Id,
    string Query,
    string ProposedAnswer,
    string GroundTruth,
    List<string> ExpectedContextKeys,
    List<string> ContextDocumentationChunks
);

/// <summary>
/// Defines the core repository abstractions for querying semantic text, navigating relationship meshes, 
/// compiling analytics counters, and tracking pipeline testing telemetry.
/// </summary>
public interface IKnowledgeRetrievalRepository
{
    /// <summary>
    /// Queries vector storage to discover high-priority, plain-text semantic document chunks matching a user prompt.
    /// </summary>
    /// <param name="userQuery">The plain-text search string submitted by the user.</param>
    /// <param name="limit">The maximum number of matching records to return.</param>
    /// <returns>A collection of matching text pieces ordered by relevance vector weights.</returns>
    Task<IEnumerable<RetrievalMatch>> SearchSemanticChunksAsync(string userQuery, int limit);

    /// <summary>
    /// Queries the knowledge store to find structural relationship paths matching conversational topics.
    /// </summary>
    /// <param name="userQuery">The analytical query text or token string used to isolate graph properties.</param>
    /// <param name="limit">The maximum limit of relational chains to extract.</param>
    /// <returns>A collection of relevant graph context entities.</returns>
    Task<IEnumerable<GraphContextChain>> SearchGraphTriplesAsync(string userQuery, int limit);

    /// <summary>
    /// Fetches the raw graph topological mesh schema from storage as a JSON document tree to support network rendering engines.
    /// </summary>
    /// <param name="filterText">An optional text criteria keyword used to prune unlinked nodes out of the returned mesh.</param>
    /// <returns>A JsonElement containing the root-level collection nodes and relational edge listings.</returns>
    Task<JsonElement> FetchRawGraphMeshJsonAsync(string filterText);

    /// <summary>
    /// Counts the total number of objects or vector nodes saved within a single schema collection class table.
    /// </summary>
    /// <param name="className">The case-sensitive name of the database collection class target.</param>
    /// <returns>The total row count recorded inside the target table space.</returns>
    Task<int> GetTotalClassCountAsync(string className);

    /// <summary>
    /// Runs grouping queries across vector schemas to compute the count of unique source parent documents tracking within a class property.
    /// </summary>
    /// <param name="className">The case-sensitive name of the database collection target.</param>
    /// <param name="groupProperty">The specific metadata property string field to group by.</param>
    /// <returns>The distinct count of parent documents tracked.</returns>
    Task<int> GetDistinctSourceCountAsync(string className, string groupProperty);

    /// <summary>
    /// Pulls raw user conversation data tables and telemetry histories out of storage.
    /// </summary>
    /// <param name="limit">The maximum number of historical log entries to extract.</param>
    /// <returns>A JSON block containing sequential interaction log parameters.</returns>
    Task<JsonElement> GetRawInteractionLogsAsync(int limit);

    /// <summary>
    /// Persists a permanent historical audit record documenting a single user query transaction.
    /// </summary>
    /// <param name="query">The user prompt text submitted to the system.</param>
    /// <param name="answer">The response string produced by the conversational service engine.</param>
    /// <param name="wasSuccessful">True if the exchange completed cleanly without user objections or platform faults.</param>
    /// <returns>An asynchronous task tracking the operation.</returns>
    Task LogInteractionAsync(string query, string answer, bool wasSuccessful);

    /// <summary>
    /// Inserts a sequence of processed software release segments into vector storage collections.
    /// </summary>
    /// <param name="nodes">The collection of software release node entities containing update descriptions.</param>
    /// <returns>An asynchronous task tracking the operation.</returns>
    Task BatchIngestReleaseNodesAsync(IEnumerable<SoftwareReleaseNode> nodes);

    /// <summary>
    /// Verifies if a specific product configuration track version has already been indexed to avoid duplicate storage commits.
    /// </summary>
    /// <param name="product">The target application name keyword (e.g., "NaviPac").</param>
    /// <param name="version">The targeted release distribution number string (e.g., "4.6.1").</param>
    /// <returns>True if the version matching criteria already exists in vector tracking schemas, otherwise false.</returns>
    Task<bool> DoesProductVersionExistAsync(string product, string version);

    /// <summary>
    /// Executes proximity queries to fetch technical documentation or software contexts using an anchor ticket's unique token.
    /// </summary>
    /// <param name="ticketId">The cross-system tracking identifier of the anchor ticket node.</param>
    /// <param name="maxResults">The maximum limit number of proximity records to fetch from the data space.</param>
    /// <returns>A collection of matching technical context results.</returns>
    Task<List<TechnicalContextSearchResult>> SearchTechnicalContextAsync(string ticketId, int maxResults);

    /// <summary>
    /// Extracts a randomized or prioritized set of validation test records matching a targeted lifecycle state criteria group.
    /// </summary>
    /// <param name="phase">The tracking phase categorization used to subset the testing pool.</param>
    /// <param name="count">The requested number of test rows to load into the execution stack.</param>
    /// <returns>A list of structured evaluation test cases.</returns>
    Task<List<EvaluationTestCase>> FetchEvaluationDeckByPhaseAsync(EvaluationPhase phase, int count);

    /// <summary>
    /// Commits a telemetry log entry documenting human-in-the-loop review insights directly into reinforcement tracking databases.
    /// </summary>
    /// <param name="log">The feedback log data model capturing user approval flags and correction adjustments.</param>
    /// <returns>An asynchronous task tracking the operation.</returns>
    Task SaveSwipeTelemetryAsync(EvaluationFeedbackLog log);
}

/// <summary>
/// Maps a single matching record discovered during cross-reference technical documentation searches.
/// </summary>
public class TechnicalContextSearchResult
{
    /// <summary>
    /// Gets or sets the unique tracking key of the matched data block in storage.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the raw, unsegmented plain-text content extracted from the matched asset node.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the database class schema name where this entry lives (e.g., "DocumentLibrary", "SoftwareReleaseNode").
    /// </summary>
    public string CollectionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the suggested network relationship verb predicate linking this matching block back to the anchor ticket (e.g., "RESOLVED_BY").
    /// </summary>
    public string SuggestedEdgeType { get; set; } = "RESOLVED_BY";
}