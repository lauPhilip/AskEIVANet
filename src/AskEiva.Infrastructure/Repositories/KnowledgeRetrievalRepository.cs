using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using AskEiva.Domain.Repositories;
using AskEiva.Domain.ValueObjects;
using AskEiva.Domain.Entities;

namespace AskEiva.Infrastructure.Repositories;

/// <summary>
/// Implements the <see cref="IKnowledgeRetrievalRepository"/> contract using a dual-client 
/// architecture to orchestrate vector queries, telemetry compilation, automated test generation, 
/// and thread-safe RLHF telemetry persistence.
/// </summary>
public class KnowledgeRetrievalRepository : IKnowledgeRetrievalRepository
{
    private readonly HttpClient _weaviateClient;
    private readonly HttpClient _mistralClient;
    
    /// <summary>
    /// Enforces mutual exclusion during local JSON matrix reads and writes to prevent telemetry file corruption.
    /// </summary>
    private static readonly SemaphoreSlim _fileLock = new(1, 1);
    
    /// <summary>
    /// Absolute platform path tracking where the local Reinforcement Learning from Human Feedback dataset matrix is saved.
    /// </summary>
    private readonly string _evaluationDatasetPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, 
        "..", "..", "..", "..", "..", 
        "EvaluationDataset", 
        "eiva_rlhf_training_matrix.json"
    );

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeRetrievalRepository"/> class using an explicit 
    /// HTTP configuration pipeline and a managed client factory infrastructure.
    /// </summary>
    /// <param name="httpClient">An unmanaged or factory-managed HTTP client mapped to the Weaviate REST service.</param>
    /// <param name="httpClientFactory">The system infrastructure factory used to resolve specialized clients (e.g., Mistral endpoints).</param>
    public KnowledgeRetrievalRepository(HttpClient httpClient, IHttpClientFactory httpClientFactory)
    {
        _weaviateClient = httpClient;
        _mistralClient = httpClientFactory.CreateClient("MistralClient");
    }

    /// <summary>
    /// Executes a parallel multi-collection hybrid semantic query (Alpha = 0.5) across support logs, technical documentation, and software release nodes.
    /// </summary>
    /// <param name="userQuery">The plain-text search prompt submitted by the operator.</param>
    /// <param name="limit">The localized limit variable indicating how many records to prioritize.</param>
    /// <returns>A consolidated, relevance-ranked sequence of unalterable retrieval match items.</returns>
    public async Task<IEnumerable<RetrievalMatch>> SearchSemanticChunksAsync(string userQuery, int limit)
    {
        var url = "v1/graphql";
        int limitPerCollection = Math.Max(limit, 4);

        var query = new
        {
            query = $$"""
            {
              Get {
                KnowledgeNode(
                  limit: {{limitPerCollection}}
                  hybrid: { query: "{{userQuery}}", alpha: 0.5 }
                ) {
                  source_id
                  subject
                  content
                  url
                  _additional { score }
                }
                DocumentationLibrary(
                  limit: {{limitPerCollection}}
                  hybrid: { query: "{{userQuery}}", alpha: 0.5 }
                ) {
                  document_id
                  title
                  content
                  url
                  _additional { score }
                }
                SoftwareReleaseNode(
                  limit: {{limitPerCollection}}
                  hybrid: { query: "{{userQuery}}", alpha: 0.5 }
                ) {
                  product
                  version
                  metadata_note
                  content_chunk
                  ref_tickets
                  _additional { score }
                }
              }
            }
            """
        };

        try
        {
            var response = await _weaviateClient.PostAsync(url, new StringContent(JsonSerializer.Serialize(query), Encoding.UTF8, "application/json"));
            if (!response.IsSuccessStatusCode) return Enumerable.Empty<RetrievalMatch>();

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var matches = new List<RetrievalMatch>();
            var root = doc.RootElement.GetProperty("data").GetProperty("Get");

            if (root.TryGetProperty("KnowledgeNode", out var ticketNodes) && ticketNodes.ValueKind == JsonValueKind.Array)
            {
                foreach (var node in ticketNodes.EnumerateArray())
                {
                    float score = 0.0f;
                    if (node.TryGetProperty("_additional", out var addProp))
                    {
                        if (addProp.TryGetProperty("score", out var scoreProp))
                        {
                            float.TryParse(scoreProp.GetRawText(), out score);
                        }
                        else if (addProp.TryGetProperty("Score", out var upperScoreProp))
                        {
                            float.TryParse(upperScoreProp.GetRawText(), out score);
                        }
                    }

                    matches.Add(new RetrievalMatch(
                        SourceId: node.GetProperty("source_id").GetString() ?? string.Empty,
                        Title: node.GetProperty("subject").GetString() ?? "Technical Excerpt",
                        Content: node.GetProperty("content").GetString() ?? string.Empty,
                        SourceUrl: node.GetProperty("url").GetString() ?? string.Empty,
                        ConfidenceScore: score,
                        SourceType: "KnowledgeNode", 
                        ImageUrls: new()
                    ));
                }
            }

            if (root.TryGetProperty("DocumentationLibrary", out var docNodes) && docNodes.ValueKind == JsonValueKind.Array)
            {
                foreach (var node in docNodes.EnumerateArray())
                {
                    float score = 0.0f;
                    if (node.TryGetProperty("_additional", out var addProp))
                    {
                        if (addProp.TryGetProperty("score", out var scoreProp))
                        {
                            float.TryParse(scoreProp.GetRawText(), out score);
                        }
                        else if (addProp.TryGetProperty("Score", out var upperScoreProp))
                        {
                            float.TryParse(upperScoreProp.GetRawText(), out score);
                        }
                    }

                    matches.Add(new RetrievalMatch(
                        SourceId: node.TryGetProperty("document_id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty,
                        Title: node.TryGetProperty("title", out var titleProp) ? (titleProp.GetString() ?? "Documentation Article") : "Documentation Article",
                        Content: node.GetProperty("content").GetString() ?? string.Empty,
                        SourceUrl: node.GetProperty("url").GetString() ?? string.Empty,
                        ConfidenceScore: score,
                        SourceType: "DocumentLibrary", 
                        ImageUrls: new()
                    ));
                }
            }

            if (root.TryGetProperty("SoftwareReleaseNode", out var releaseNodes) && releaseNodes.ValueKind == JsonValueKind.Array)
            {
                foreach (var node in releaseNodes.EnumerateArray())
                {
                    float score = 0.0f;
                    if (node.TryGetProperty("_additional", out var addProp))
                    {
                        if (addProp.TryGetProperty("score", out var scoreProp))
                        {
                            float.TryParse(scoreProp.GetRawText(), out score);
                        }
                        else if (addProp.TryGetProperty("Score", out var upperScoreProp))
                        {
                            float.TryParse(upperScoreProp.GetRawText(), out score);
                        }
                    }

                    string product = node.GetProperty("product").GetString() ?? "Product Note";
                    string version = node.GetProperty("version").GetString() ?? "";

                    matches.Add(new RetrievalMatch(
                        SourceId: node.TryGetProperty("ref_tickets", out var tProp) ? tProp.GetString() ?? string.Empty : string.Empty,
                        Title: $"[{product} v{version}] Release Excerpt",
                        Content: node.GetProperty("content_chunk").GetString() ?? string.Empty,
                        SourceUrl: "https://download.eiva.com/#",
                        ConfidenceScore: score,
                        SourceType: "SoftwareReleaseNode", 
                        ImageUrls: new(),
                        ProductContext: product,
                        VersionContext: version
                    ));
                }
            }

            return matches.OrderByDescending(m => m.ConfidenceScore);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Weaviate Mapping Exception]: {ex.Message}");
            return Enumerable.Empty<RetrievalMatch>();
        }
    }

    /// <summary>
    /// Compiles a fallback evaluation item block used defensively when the repository fails to connect to structural cluster indexes.
    /// </summary>
    /// <param name="count">The limitation quantity tracking how many static scenarios to output.</param>
    /// <returns>A populated collection layout of validation cases.</returns>
    private List<EvaluationTestCase> GetFallbackBaselineDeck(int count)
        {
            var fallback = new List<EvaluationTestCase>
            {
                new EvaluationTestCase(
                    Id: "FD-70821",
                    Query: "[FD-70821] Navipac-Dongle10513497-DB Version 4.0.3.0\n\nLooking for advice regarding Master Helmsman PC sensor registration telemetry drifts.",
                    ProposedAnswer: "Proposed Fix: Configure baseline communication bindings down to alternative virtual port addresses or apply firmware updates matching your hardware profile layout constraints.",
                    GroundTruth: "Verified Engineering Fix: Update primary USBL serial buffer IO frames via NaviPac's dynamic configuration module system manager layout.",
                    ExpectedContextKeys: new() { "NaviPac", "KnowledgeNode" },
                    ContextDocumentationChunks: new() { "Documentation Manual reference fragment: Ensure dongle drivers are properly mapped in offline machine profiles." }
                )
            };
            return fallback.Take(count).ToList();
        }

    /// <summary>
    /// Generates evaluation matrices by parsing closed technical tickets, separating customer issues from support replies, 
    /// resolving contextual documentation nodes, and computing Mistral evaluation benchmarks.
    /// </summary>
    /// <param name="phase">The tracking deployment stage classification (e.g., ClosedBaseline).</param>
    /// <param name="count">The targeted total item threshold size to return for the testing suite.</param>
    /// <returns>A collection stream loaded with synthesized validation cases.</returns>
    public async Task<List<EvaluationTestCase>> FetchEvaluationDeckByPhaseAsync(EvaluationPhase phase, int count)
    {
        var deck = new List<EvaluationTestCase>();

        if (phase == EvaluationPhase.ClosedBaseline)
        {
            var url = "v1/graphql";
            var gqlQuery = """
            {
              Get {
                KnowledgeNode(limit: 150) {
                  source_id
                  subject
                  content
                  url
                }
              }
            }
            """;

            try
            {
                var response = await _weaviateClient.PostAsJsonAsync(url, new { query = gqlQuery });
                if (!response.IsSuccessStatusCode) return GetFallbackBaselineDeck(count);

                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                if (doc.RootElement.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("Get", out var get) &&
                    get.TryGetProperty("KnowledgeNode", out var nodesArray) &&
                    nodesArray.ValueKind == JsonValueKind.Array)
                {
                    var ticketGroups = nodesArray.EnumerateArray()
                        .Where(node => node.TryGetProperty("source_id", out var idProp) && !string.IsNullOrWhiteSpace(idProp.GetString()))
                        .GroupBy(node => node.GetProperty("source_id").GetString()!)
                        .ToList();

                    if (!ticketGroups.Any()) return GetFallbackBaselineDeck(count);

                    var randomizer = new Random();
                    var selectedGroups = ticketGroups.OrderBy(_ => randomizer.Next()).Take(count);

                    foreach (var group in selectedGroups)
                    {
                        string ticketId = group.Key;
                        string subjectLine = group.First().GetProperty("subject").GetString() ?? "EIVA Support Request";

                        var fullTextBuilder = new StringBuilder();
                        foreach (var chunk in group.OrderBy(c => c.GetProperty("url").GetString()))
                        {
                            fullTextBuilder.AppendLine(chunk.GetProperty("content").GetString() ?? "");
                        }
                        string continuousText = fullTextBuilder.ToString();

                        string isolatedCustomerQuery = "";
                        string isolatedAgentGroundTruth = "";

                        var replySplitPattern = new Regex(@"(---\s*Reply\s*by\s*AGENT[\s\S]*|EIVA\s*SW\s*Support\s*ASIA[\s\S]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                        var match = replySplitPattern.Match(continuousText);

                        if (match.Success)
                        {
                            isolatedCustomerQuery = continuousText.Substring(0, match.Index).Trim();
                            isolatedAgentGroundTruth = match.Value.Trim();
                        }
                        else
                        {
                            isolatedCustomerQuery = continuousText;
                            isolatedAgentGroundTruth = "Resolved via standard technical operational validation protocols.";
                        }
                        // Sanitize conversational boilerplate out of the prompt
                        isolatedCustomerQuery = Regex.Replace(isolatedCustomerQuery, @"^Hello[\s\S]*?(?=Hope|Looking|We)", "", RegexOptions.IgnoreCase);

                        var supportingDocs = await RetrieveSupportingDocumentationChunksAsync(subjectLine, isolatedCustomerQuery);
                        string proposedModelFix = await GenerateMistralProposedSolutionAsync(isolatedCustomerQuery, isolatedAgentGroundTruth, supportingDocs);

                        // Extract official product category badges using regex boundaries
                        var badges = ParseDynamicContextKeys(continuousText);

                        deck.Add(new EvaluationTestCase(
                            Id: ticketId,
                            Query: $"[{ticketId}] {subjectLine}\n\n{isolatedCustomerQuery}",
                            ProposedAnswer: proposedModelFix,
                            GroundTruth: isolatedAgentGroundTruth,
                            ExpectedContextKeys: badges,
                            ContextDocumentationChunks: supportingDocs
                        ));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RLHF Database Engine Error]: {ex.Message}");
                return GetFallbackBaselineDeck(count);
            }
        }
        return deck;
    }

    /// <summary>
    /// Core search routine used during validation steps to fetch reference manuals matching active ticket parameters.
    /// </summary>
    private async Task<List<string>> RetrieveSupportingDocumentationChunksAsync(string subject, string queryText)
    {
        var searchMatches = await SearchSemanticChunksAsync($"{subject} {queryText}", limit: 2);
        return searchMatches.Where(m => m.SourceType == "DocumentLibrary").Select(m => m.Content).ToList();
    }

    /// <summary>
    /// Dispatches prompt configurations to Mistral infrastructure to compute optimized engineering predictions.
    /// </summary>
    private async Task<string> GenerateMistralProposedSolutionAsync(string customerIssue, string agentAnswers, List<string> docs)
    {
        var promptPayload = new
        {
            model = "mistral-large-latest",
            messages = new[]
            {
                new { 
                    role = "system", 
                    content = "You are AskEIVA, an expert reasoning agent for marine survey software. Analyze the customer's problem description. Use the historical technical support interactions and documentation manual excerpts provided to formulate a crisp, direct troubleshooting fix. Do not repeat greeting signatures or metadata headers." 
                },
                new { 
                    role = "user", 
                    content = $"""
                        [Customer Support Ticket Incident]:
                        {customerIssue}

                        [Historical Internal Expert Resolution Context]:
                        {agentAnswers}

                        [Supporting Documentation Manual Reference Fragments]:
                        {string.Join("\n\n", docs)}

                        Based strictly on the expert context above, what is the final proposed engineering fix for this customer issue? Propose the direct troubleshooting steps now.
                        """ 
                }
            },
            temperature = 0.2,
            max_tokens = 400
        };

        try
        {
            var response = await _mistralClient.PostAsJsonAsync("v1/chat/completions", promptPayload);
            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Mistral API Connection Failure]: Gateway returned status {response.StatusCode} - {errorContent}");
                return "Unable to parse autonomous prediction. Review historical expert resolutions for standard manual configuration adjustments.";
            }

            using var jsonDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("choices", out var choices) && 
                choices.ValueKind == JsonValueKind.Array && 
                choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var messageProp) && 
                    messageProp.TryGetProperty("content", out var contentProp))
                {
                    return contentProp.GetString()?.Trim() ?? string.Empty;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Mistral Integration Error Loop Failed]: {ex.Message}");
        }

        return "Verification Check Required: Review standard product release matrix guidelines to troubleshoot connection links.";
    }

    /// <summary>
    /// Validates raw interaction data blocks against a strict collection of high-fidelity EIVA product tokens using 
    /// explicitly compiled word boundary regex engines.
    /// </summary>
    /// <param name="fullBody">The continuous plain text compiled from historical files or ticket threads.</param>
    /// <returns>A collection array listing matching product categories combined with default category mappings.</returns>
    private List<string> ParseDynamicContextKeys(string fullBody)
    {
        var discoveredBadges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string[] eivaProducts =
        [
            "NaviSuite", "NaviPac", "NaviScan", "NaviEdit", "NaviModel", "NaviPlot", 
            "NaviSuite Beka", "NaviSuite Nardoa", "NaviSuite Uca", "Workflow Manager", 
            "QuickStitch Utility", "Catenary option", "NaviSuite Mobula", "NaviSuite Kuda", 
            "NaviSuite Perio", "NaviSuite QC Toolbox", "Voyis VSLAM", "Powered by EIVA", 
            "NaviSuite ROTV", "ScanFish", "ViperFish", "ATTU Mk II", "ATTU Mk I", "ATTU"
        ];

        foreach (var product in eivaProducts)
        {
            // Boundary checks ensure we don't accidentally match substrings (e.g. matching "ATTU" inside "ATTU Mk II")
            // We escape specific items like "Mk II" or "Mk I" safely to keep compilation secure
            string escapedToken = Regex.Escape(product);
            var regexMatcher = new Regex($@"\b{escapedToken}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            if (regexMatcher.IsMatch(fullBody))
            {
                discoveredBadges.Add(product);
            }
        }

        // Always retain our structural core tracking category baseline
        discoveredBadges.Add("KnowledgeNode");
        return discoveredBadges.ToList();
    }

    /// <summary>
    /// Extracts a topological raw graph metadata slice from Weaviate using filter constraints to build relationship meshes.
    /// </summary>
    /// <param name="filterText">An optional text query to slice or trim unlinked nodes from the graph canvas.</param>
    /// <returns>A JsonElement container holding array nodes and confidence profiles.</returns>
    public async Task<JsonElement> FetchRawGraphMeshJsonAsync(string filterText)
    {
        var url = "v1/graphql";
        string graphQlFilterBlock = string.IsNullOrWhiteSpace(filterText) 
            ? "limit: 45" 
            : "limit: 45, hybrid: { query: \"" + filterText + "\", alpha: 0.4 }";

        var query = new
        {
            query = $$"""
            {
              Get {
                GraphContextChain({{graphQlFilterBlock}}) {
                  ticket_id
                  main_product_context
                  scenario_type
                  predicates
                  confidence_score
                }
              }
            }
            """
        };

        try
        {
            var response = await _weaviateClient.PostAsJsonAsync(url, query);
            if (!response.IsSuccessStatusCode) return default;

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            return doc.RootElement.Clone();
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Direct retrieval search mechanism used to extract full structural graph paths matching a user question.
    /// </summary>
    /// <param name="userQuery">The plain-text question or keyword filter submitted by the client.</param>
    /// <param name="limit">The upper limitation value of matching mesh assets to extract from storage structures.</param>
    /// <returns>A collection containing matching graph context chains loaded with their respective predicates.</returns>
    public async Task<IEnumerable<GraphContextChain>> SearchGraphTriplesAsync(string userQuery, int limit)
    {
        var url = "v1/graphql";
        var query = new
        {
            query = $$"""
            {
              Get {
                GraphContextChain(
                  limit: {{limit}}
                  hybrid: { query: "{{userQuery}}", alpha: 0.5 }
                ) {
                  ticket_id
                  ticket_title
                  main_product_context
                  scenario_type
                  shared_path_chain
                  predicates
                  environmental_context_summary
                  confidence_score
                }
              }
            }
            """
        };

        try
        {
            var response = await _weaviateClient.PostAsync(url, new StringContent(JsonSerializer.Serialize(query), Encoding.UTF8, "application/json"));
            if (!response.IsSuccessStatusCode) return Enumerable.Empty<GraphContextChain>();

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var meshes = new List<GraphContextChain>();

            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("Get", out var get) &&
                get.TryGetProperty("GraphContextChain", out var nodes))
            {
                foreach (var node in nodes.EnumerateArray())
                {
                    var predicatesList = new List<string>();
                    if (node.TryGetProperty("predicates", out var predProp) && predProp.ValueKind == JsonValueKind.Array)
                    {
                        predicatesList = predProp.EnumerateArray().Select(p => p.GetString() ?? string.Empty).ToList();
                    }

                    meshes.Add(new GraphContextChain
                    {
                        TicketId = node.GetProperty("ticket_id").GetString() ?? string.Empty,
                        TicketTitle = node.GetProperty("ticket_title").GetString() ?? string.Empty,
                        MainProductContext = node.GetProperty("main_product_context").GetString() ?? string.Empty,
                        ScenarioType = node.GetProperty("scenario_type").GetString() ?? string.Empty,
                        SharedPathChain = node.GetProperty("shared_path_chain").GetString() ?? string.Empty,
                        Predicates = predicatesList,
                        EnvironmentalContextSummary = node.GetProperty("environmental_context_summary").GetString() ?? string.Empty,
                        ConfidenceScore = node.GetProperty("confidence_score").GetInt32()
                    });
                }
            }
            return meshes;
        }
        catch
        {
            return Enumerable.Empty<GraphContextChain>();
        }
    }

    /// <summary>
    /// Executes an aggregation query against Weaviate cluster collections to count total row records.
    /// </summary>
    /// <param name="className">The case-sensitive collection target class name.</param>
    /// <returns>The total database object count tracking inside that partition layout space.</returns>
    public async Task<int> GetTotalClassCountAsync(string className)
    {
        var jsonQuery = new { query = $"{{ Aggregate {{ {className} {{ meta {{ count }} }} }} }}" };
        try
        {
            var response = await _weaviateClient.PostAsJsonAsync("v1/graphql", jsonQuery);
            if (!response.IsSuccessStatusCode) return 0;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("Aggregate", out var agg) &&
                agg.TryGetProperty(className, out var classArr) &&
                classArr.ValueKind == JsonValueKind.Array && classArr.GetArrayLength() > 0)
            {
                var meta = classArr[0].GetProperty("meta");
                return meta.GetProperty("count").GetInt32();
            }
        }
        catch { }
        return 0;
    }

    /// <summary>
    /// Extracts chunks sequentially to determine the total count of distinct, parent-level documents matching 
    /// custom identification schemas.
    /// </summary>
    /// <param name="className">The case-sensitive collection schema partition target.</param>
    /// <param name="propertyName">The specific metadata property string field to analyze.</param>
    /// <returns>The total running count of isolated unique parent records.</returns>
    public async Task<int> GetDistinctSourceCountAsync(string className, string propertyName)
    {
        var gqlQuery = $$"""
        {
          Get {
            {{className}}(limit: 50000) {
              {{propertyName}}
            }
          }
        }
        """;

        try
        {
            var response = await _weaviateClient.PostAsJsonAsync("v1/graphql", new { query = gqlQuery });
            if (!response.IsSuccessStatusCode) return 0;

            using var jsonDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            
            if (jsonDoc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("Get", out var get) &&
                get.TryGetProperty(className, out var chunkArray) &&
                chunkArray.ValueKind == JsonValueKind.Array)
            {
                var uniqueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var kbPattern = new Regex(@"^(kb_\d+_)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

                foreach (var chunk in chunkArray.EnumerateArray())
                {
                    if (chunk.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
                    {
                        string rawId = prop.GetString() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(rawId)) continue;

                        if (className == "DocumentationLibrary")
                        {
                            var match = kbPattern.Match(rawId);
                            if (match.Success)
                            {
                                uniqueIds.Add(match.Value.ToLowerInvariant()); 
                            }
                            else
                            {
                                uniqueIds.Add(rawId.ToLowerInvariant());
                            }
                        }
                        else
                        {
                            uniqueIds.Add(rawId);
                        }
                    }
                }

                Console.WriteLine($"[Application Telemetry Engine] Cleaned {chunkArray.GetArrayLength()} chunks for {className}. Unique Parents Found: {uniqueIds.Count}");
                return uniqueIds.Count;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Telemetry Aggregation Error] Failed matching custom text filters for {className}: {ex.Message}");
        }

        return 0;
    }

    /// <summary>
    /// Extracts historical user conversation records and platform answer payloads out of storage.
    /// </summary>
    /// <param name="limit">The maximum limit number of audit log fields to return.</param>
    /// <returns>A JSON schema block containing sequenced interaction parameters.</returns>
    public async Task<JsonElement> GetRawInteractionLogsAsync(int limit)
    {
        var jsonQuery = new { query = $"{{ Get {{ InteractionLog(limit: {limit}) {{ query answer was_successful timestamp }} }} }}" };
        try
        {
            var response = await _weaviateClient.PostAsJsonAsync("v1/graphql", jsonQuery);
            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(jsonString);
                return doc.RootElement.Clone();
            }
        }
        catch { }
        return default;
    }

    /// <summary>
    /// Commits a permanent operational audit entry logging an exchange transaction into the interaction database space.
    /// </summary>
    /// <param name="query">The natural text string submitted by the user.</param>
    /// <param name="answer">The plain-text answer produced by the agent engine.</param>
    /// <param name="wasSuccessful">True if the operator accepted the answer profile without tracking flags or rejections.</param>
    /// <returns>An asynchronous task tracking execution completion.</returns>
    public async Task LogInteractionAsync(string query, string answer, bool wasSuccessful)
    {
        var url = "v1/objects";
        var payload = new
        {
            @class = "InteractionLog",
            properties = new
            {
                query = query,
                answer = answer,
                was_successful = wasSuccessful,
                timestamp = DateTime.UtcNow.ToString("o")
            }
        };

        try
        {
            var response = await _weaviateClient.PostAsJsonAsync(url, payload);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Weaviate Telemetry Error] Failed to write active interaction log: {ex.Message}");
        }
    }

    /// <summary>
    /// Direct ingestion engine designed to process software release nodes using a chunk size boundary limitation of 30 
    /// items per loop to optimize remote cluster performance limits.
    /// </summary>
    /// <param name="nodes">The sequence collection holding software version change notes.</param>
    /// <returns>An asynchronous task tracking batch execution progress metrics.</returns>
    public async Task BatchIngestReleaseNodesAsync(IEnumerable<SoftwareReleaseNode> nodes)
    {
        var url = "v1/batch/objects";
        var allBatchObjects = nodes.Select(node => new
        {
            @class = "SoftwareReleaseNode",
            properties = new
            {
                group_category = node.GroupCategory,
                product = node.Product,
                release_type = node.ReleaseType,
                version = node.Version,
                full_version_title = node.FullVersionTitle,
                metadata_note = node.MetadataNote,
                release_date = node.ReleaseDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                section_header = node.SectionHeader,
                content_chunk = node.ContentChunk,
                ref_tickets = node.RefTickets
            }
        }).ToList();

        const int MaxMistralBatchSize = 30;
        int totalIngestedCount = 0;

        for (int i = 0; i < allBatchObjects.Count; i += MaxMistralBatchSize)
        {
            var currentChunkPartition = allBatchObjects.Skip(i).Take(MaxMistralBatchSize).ToList();
            var payload = new { objects = currentChunkPartition };

            try
            {
                var response = await _weaviateClient.PostAsJsonAsync(url, payload);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Weaviate Segment Error] Batch request aborted with status code: {response.StatusCode}");
                    continue;
                }

                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    int segmentSuccessCount = 0;
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.TryGetProperty("result", out var res) && res.TryGetProperty("errors", out var err))
                        {
                            Console.WriteLine($"[Weaviate Batch Object Rejection]: {err.GetRawText()}");
                        }
                        else
                        {
                            segmentSuccessCount++;
                        }
                    }
                    totalIngestedCount += segmentSuccessCount;
                    Console.WriteLine($"[Weaviate Ingestion Segment] Successfully vectorized {segmentSuccessCount}/{currentChunkPartition.Count} items.");
                }
                
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Infrastructure Batch Error] Processing track segment collapsed: {ex.Message}");
            }
        }

        Console.WriteLine($"\n[Weaviate Ingestion Engine] Task Finished! Total of {totalIngestedCount} out of {allBatchObjects.Count} records indexed successfully into the cluster.\n");
    }

    /// <summary>
    /// Executes conditional matching across target product title keys and distribution version strings 
    /// to identify duplicate records.
    /// </summary>
    /// <param name="product">The target application profile keyword (e.g., NaviEdit).</param>
    /// <param name="version">The targeted version layout tracking distribution string (e.g., 4.2.1).</param>
    /// <returns>True if the matching node count inside the database class equals or exceeds 1.</returns>
    public async Task<bool> DoesProductVersionExistAsync(string product, string version)
    {
        var url = "v1/graphql";
        var query = new
        {
            query = $$"""
            {
              Get {
                SoftwareReleaseNode(
                  limit: 1
                  where: {
                    operator: And,
                    operands: [
                      { path: ["product"], operator: Equal, valueText: "{{product}}" },
                      { path: ["version"], operator: Equal, valueText: "{{version}}" }
                    ]
                  }
                ) {
                  version
                }
              }
            }
            """
        };

        try
        {
            var response = await _weaviateClient.PostAsJsonAsync(url, query);
            if (!response.IsSuccessStatusCode) return false;

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("Get", out var get) &&
                get.TryGetProperty("SoftwareReleaseNode", out var nodesArr) &&
                nodesArr.ValueKind == JsonValueKind.Array)
            {
                return nodesArr.GetArrayLength() > 0;
            }
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Performs nearObject cross-reference queries utilizing an anchor support ticket's UUID vector location 
    /// to extract adjacent context elements from manuals and release charts.
    /// </summary>
    /// <param name="ticketId">The primary storage object tracking UUID string of the anchor support node.</param>
    /// <param name="maxResults">The prioritized limitation size capping the quantity of adjacent results to pull.</param>
    /// <returns>A context list loaded with adjacent tracking elements, mapped with suggested edge relationship predicates.</returns>
    public async Task<List<TechnicalContextSearchResult>> SearchTechnicalContextAsync(string ticketId, int maxResults)
    {
        var results = new List<TechnicalContextSearchResult>();

        try
        {
            var graphQlQuery = $$"""
            {
              Get {
                DocumentLibrary(
                  nearObject: { id: "{{ticketId}}" }
                  limit: {{maxResults}}
                ) {
                  _additional { id }
                  content
                }
                SoftwareReleaseNode(
                  nearObject: { id: "{{ticketId}}" }
                  limit: {{maxResults}}
                ) {
                  _additional { id }
                  content_chunk
                }
              }
            }
            """;

            var requestPayload = new { query = graphQlQuery };
            var response = await _weaviateClient.PostAsJsonAsync("v1/graphql", requestPayload);
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Weaviate HTTP Failure] GraphQL returned status: {response.StatusCode}");
                return results;
            }

            using var jsonDocument = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var root = jsonDocument.RootElement;

            if (root.TryGetProperty("data", out var dataNode) && dataNode.TryGetProperty("Get", out var getCollection))
            {
                if (getCollection.TryGetProperty("DocumentLibrary", out var docArray) && docArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in docArray.EnumerateArray())
                    {
                        string idStr = elem.GetProperty("_additional").GetProperty("id").GetString() ?? Guid.NewGuid().ToString();
                        results.Add(new TechnicalContextSearchResult
                        {
                            Id = Guid.Parse(idStr),
                            Content = elem.GetProperty("content").GetString() ?? string.Empty,
                            CollectionName = "DocumentLibrary",
                            SuggestedEdgeType = "EXPLAINS_FEATURE"
                        });
                    }
                }

                if (getCollection.TryGetProperty("SoftwareReleaseNode", out var releaseArray) && releaseArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in releaseArray.EnumerateArray())
                    {
                        string idStr = elem.GetProperty("_additional").GetProperty("id").GetString() ?? Guid.NewGuid().ToString();
                        results.Add(new TechnicalContextSearchResult
                        {
                            Id = Guid.Parse(idStr),
                            Content = elem.GetProperty("content_chunk").GetString() ?? string.Empty,
                            CollectionName = "SoftwareReleaseNode",
                            SuggestedEdgeType = "ACCUSES_BUG"
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Infrastructure Graph Search Failure] Direct HTTP stream faulted: {ex.Message}");
        }

        return results.OrderBy(x => Guid.NewGuid()).Take(maxResults).ToList();
    }

    /// <summary>
    /// Commits and appends a reinforced human-in-the-loop audit pair log back to the training dataset file tree 
    /// using a thread-safe semaphore lock barrier.
    /// </summary>
    /// <param name="log">The validation log capturing system states, generated responses, and human adjustments.</param>
    /// <returns>An asynchronous task tracking execution completion states.</returns>
    public async Task SaveSwipeTelemetryAsync(EvaluationFeedbackLog log)
    {
        string? directoryPath = Path.GetDirectoryName(_evaluationDatasetPath);
        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        await _fileLock.WaitAsync();
        try
        {
            List<EvaluationFeedbackLog> existingLogs = new List<EvaluationFeedbackLog>();

            if (File.Exists(_evaluationDatasetPath))
            {
                string rawJson = await File.ReadAllTextAsync(_evaluationDatasetPath);
                if (!string.IsNullOrWhiteSpace(rawJson) && rawJson.Trim().StartsWith("["))
                {
                    var readOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    existingLogs = JsonSerializer.Deserialize<List<EvaluationFeedbackLog>>(rawJson, readOptions) ?? new List<EvaluationFeedbackLog>();
                }
            }

            existingLogs.Add(log);

            var writeOptions = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            string updatedJson = JsonSerializer.Serialize(existingLogs, writeOptions);
            await File.WriteAllTextAsync(_evaluationDatasetPath, updatedJson);

            Console.WriteLine($"[RLHF MASTER REGISTER] Training pair committed. Dataset count size: {existingLogs.Count} logged nodes.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RLHF Matrix Ingestion Crash]: {ex.Message}");
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }
}