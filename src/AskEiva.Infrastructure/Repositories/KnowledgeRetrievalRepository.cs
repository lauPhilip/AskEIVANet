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

public class KnowledgeRetrievalRepository : IKnowledgeRetrievalRepository
{
    private readonly HttpClient _httpClient;
    
    // 💡 THE CONCURRENCY GUARD: Prevents cross-circuit thread lock collisions when multiple users swipe simultaneously
    private static readonly SemaphoreSlim _fileLock = new(1, 1);
    
    // 💡 THE REINFORCEMENT LEARNING DIRECTORY CORNERSTONE
    private readonly string _evaluationDatasetPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, 
        "..", "..", "..", "..", "..", 
        "EvaluationDataset", 
        "eiva_rlhf_training_matrix.json"
    );

    public KnowledgeRetrievalRepository(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<RetrievalMatch>> SearchSemanticChunksAsync(string userQuery, int limit)
    {
        var url = "v1/graphql";
        
        // Ensure we query enough records from each array type to get a balanced cross-functional mix
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
                DocumentLibrary(
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
            var response = await _httpClient.PostAsync(url, new StringContent(JsonSerializer.Serialize(query), Encoding.UTF8, "application/json"));
            if (!response.IsSuccessStatusCode) return Enumerable.Empty<RetrievalMatch>();

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var matches = new List<RetrievalMatch>();
            var root = doc.RootElement.GetProperty("data").GetProperty("Get");

            // 1. Parse ticket-based KnowledgeNodes
            if (root.TryGetProperty("KnowledgeNode", out var ticketNodes) && ticketNodes.ValueKind == JsonValueKind.Array)
            {
                foreach (var node in ticketNodes.EnumerateArray())
                {
                    float.TryParse(node.GetProperty("_additional").GetProperty("score").GetRawText(), out var score);
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

            // 2. Parse documentation-based DocumentLibraries
            if (root.TryGetProperty("DocumentLibrary", out var docNodes) && docNodes.ValueKind == JsonValueKind.Array)
            {
                foreach (var node in docNodes.EnumerateArray())
                {
                    float.TryParse(node.GetProperty("_additional").GetProperty("score").GetRawText(), out var score);
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

            // 3. Parse Release Notes data blocks dynamically
            if (root.TryGetProperty("SoftwareReleaseNode", out var releaseNodes) && releaseNodes.ValueKind == JsonValueKind.Array)
            {
                foreach (var node in releaseNodes.EnumerateArray())
                {
                    float.TryParse(node.GetProperty("_additional").GetProperty("score").GetRawText(), out var score);
                    string product = node.GetProperty("product").GetString() ?? "Product Note";
                    string version = node.GetProperty("version").GetString() ?? "";
                    string note = node.TryGetProperty("metadata_note", out var nProp) ? nProp.GetString() ?? string.Empty : string.Empty;

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

public async Task<List<EvaluationTestCase>> FetchEvaluationDeckByPhaseAsync(EvaluationPhase phase, int count)
    {
        var deck = new List<EvaluationTestCase>();

        // 💡 PHASE 1: Closed Baseline Evaluation Mode
        if (phase == EvaluationPhase.ClosedBaseline)
        {
            var url = "v1/graphql";

            // 1. GraphQL Query: Fetch chunks from KnowledgeNode. 
            // We read a large block (limit: 300) so we can group fragments by their parent 'source_id'
            var gqlQuery = """
            {
              Get {
                KnowledgeNode(limit: 300) {
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
                var response = await _httpClient.PostAsJsonAsync(url, new { query = gqlQuery });
                if (!response.IsSuccessStatusCode) return GetFallbackBaselineDeck(count);

                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                
                if (doc.RootElement.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("Get", out var get) &&
                    get.TryGetProperty("KnowledgeNode", out var nodesArray) &&
                    nodesArray.ValueKind == JsonValueKind.Array)
                {
                    // 2. Group the individual database text chunks by their parent Ticket ID (source_id)
                    var ticketGroups = nodesArray.EnumerateArray()
                        .Where(node => node.TryGetProperty("source_id", out var idProp) && !string.IsNullOrWhiteSpace(idProp.GetString()))
                        .GroupBy(node => node.GetProperty("source_id").GetString()!)
                        .ToList();

                    if (!ticketGroups.Any()) return GetFallbackBaselineDeck(count);

                    // 3. Randomize the groups to ensure engineers get a fresh mix of test cards each run
                    var randomizer = new Random();
                    var randomizedGroups = ticketGroups.OrderBy(_ => randomizer.Next()).Take(count);

                    foreach (var group in randomizedGroups)
                    {
                        string ticketId = group.Key;
                        
                        // Extract the email subject line from the first chunk to use as a summary header
                        string subjectLine = group.First().TryGetProperty("subject", out var subProp) 
                            ? subProp.GetString() ?? "EIVA Support Interaction" 
                            : "EIVA Support Interaction";

                        // 4. Stitch the fragmented text chunks back together into a single, cohesive message body
                        var fullTextBuilder = new StringBuilder();
                        foreach (var chunk in group.OrderBy(c => c.GetProperty("url").GetString())) 
                        {
                            string chunkContent = chunk.TryGetProperty("content", out var contentProp) ? contentProp.GetString() ?? "" : "";
                            fullTextBuilder.AppendLine(chunkContent);
                        }

                        string continuousText = fullTextBuilder.ToString();

                        // 5. Parse the stitched text to separate the customer's problem from the engineer's solution
                        string isolatedQuery = ExtractCustomerProblemText(continuousText, subjectLine, ticketId);
                        string isolatedGroundTruth = ExtractVerifiedResolutionText(continuousText);

                        // 6. Generate matching search context tags dynamically based on the text content
                        var dynamicContextKeys = ParseDynamicContextKeys(continuousText);

                        deck.Add(new EvaluationTestCase(
                            Query: isolatedQuery,
                            ExpectedContextKeys: dynamicContextKeys,
                            GroundTruthAnswer: isolatedGroundTruth
                        ));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RLHF Engine Database Fetch Failure]: {ex.Message}. Falling back to standard suite vectors.");
                return GetFallbackBaselineDeck(count);
            }
        }
        else
        {
            // 💡 PHASE 2: Live Open Assistance Triage Mode
            deck.Add(new EvaluationTestCase(
                Query: "[LIVE TRIAGE QUEUE] Master Helmsman PC experiencing frequent desync issues with remote slave units on the survey network.",
                ExpectedContextKeys: new() { "NAVIPAC", "HELMSMAN", "DESYNC" },
                GroundTruthAnswer: "Suggested Fix: Inspect the Toppings folder synchronization interval settings and verify that file deletion tracking is active on remote nodes."
            ));
        }

        return deck.Any() ? deck : GetFallbackBaselineDeck(count);
    }

    // --- PARSING & CLEANING HELPER UTILITIES ---

    private string ExtractCustomerProblemText(string fullBody, string subject, string ticketId)
    {
        // If the text contains support thread headers, extract the initial message body
        if (fullBody.Contains("From:", StringComparison.OrdinalIgnoreCase) || fullBody.Contains("Looking for some advice", StringComparison.OrdinalIgnoreCase))
        {
            int index = fullBody.IndexOf("Regards", StringComparison.OrdinalIgnoreCase);
            if (index > 0) return $"[{ticketId}] {subject}\n\n" + fullBody.Substring(0, index + 7).Trim();
        }
        
        // Fallback: Return a clean subset of the text if it is too long
        return $"[{ticketId}] {subject}\n\n" + (fullBody.Length > 500 ? fullBody.Substring(0, 500) + "..." : fullBody).Trim();
    }

    private string ExtractVerifiedResolutionText(string fullBody)
    {
        // Locate common response signature blocks used by the support team
        string marker = "Best regards / Med venlig hilsen";
        if (fullBody.Contains(marker, StringComparison.OrdinalIgnoreCase))
        {
            int markerIdx = fullBody.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            int startIdx = Math.Max(0, markerIdx - 350);
            return "Verified Engineering Fix:\n" + fullBody.Substring(startIdx, markerIdx - startIdx).Trim();
        }

        // Alternative check: Search for text indicators that pinpoint the closing solution
        int fixIdx = fullBody.LastIndexOf("fixed in", StringComparison.OrdinalIgnoreCase);
        if (fixIdx > 0) return "Verified Engineering Fix:\n" + fullBody.Substring(fixIdx).Trim();

        return "Verified Engineering Fix:\nReview log transaction reference histories to apply the matching firmware or driver update patch.";
    }

    private List<string> ParseDynamicContextKeys(string fullBody)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Scan text content to assign target product badges automatically
        if (fullBody.Contains("NaviPac", StringComparison.OrdinalIgnoreCase)) tags.Add("NAVIPAC");
        if (fullBody.Contains("NaviScan", StringComparison.OrdinalIgnoreCase)) tags.Add("NAVISCAN");
        if (fullBody.Contains("NaviModel", StringComparison.OrdinalIgnoreCase)) tags.Add("NAVIMODEL");
        if (fullBody.Contains("Helmsman", StringComparison.OrdinalIgnoreCase)) tags.Add("HELMSMAN");
        if (fullBody.Contains("Toppings", StringComparison.OrdinalIgnoreCase)) tags.Add("TOPPINGS");
        
        tags.Add("KNOWLEDGEnode");
        return tags.ToList();
    }

    private List<EvaluationTestCase> GetFallbackBaselineDeck(int count)
    {
        // Keeps a reliable fallback list ready to ensure the interface never crashes if Weaviate is offline
        var fallback = new List<EvaluationTestCase>
        {
            new EvaluationTestCase("Timing drift faults on NaviPac 4.13 telemetry interface arrays", new() { "NAVIPAC", "SCANFISH", "KNOWLEDGEnode" }, "Configure connection links parameters via NaviPac's dynamic configuration module system manager layout, explicitly forcing the primary USBL serial buffer IO frame tracking port to open.")
        };
        return fallback.Take(count).ToList();
    }

public async Task SaveSwipeTelemetryAsync(EvaluationFeedbackLog log)
    {
        string? directoryPath = Path.GetDirectoryName(_evaluationDatasetPath);
        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        // Acquire the thread gate token
        await _fileLock.WaitAsync();
        try
        {
            List<EvaluationFeedbackLog> existingLogs = new List<EvaluationFeedbackLog>();

            // 1. Read existing historical logs with bulletproof relaxed casing rules
            if (File.Exists(_evaluationDatasetPath))
            {
                try
                {
                    string rawJson = await File.ReadAllTextAsync(_evaluationDatasetPath);
                    if (!string.IsNullOrWhiteSpace(rawJson) && rawJson.Trim().StartsWith("["))
                    {
                        var readOptions = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true, // 💡 CRITICAL: Prevents case mismatch from returning null!
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        };
                        
                        var deserialized = JsonSerializer.Deserialize<List<EvaluationFeedbackLog>>(rawJson, readOptions);
                        if (deserialized != null)
                        {
                            existingLogs = deserialized;
                        }
                    }
                }
                catch (JsonException)
                {
                    // If the JSON is somehow malformed, don't crash, just log it or back it up
                    Console.WriteLine("[RLHF ENGINE WARNING] JSON file was malformed. Re-initializing array boundary.");
                }
            }

            // 2. Append the new human telemetry record
            existingLogs.Add(log);

            // 3. Serialize the full history array clean back to disk
            var writeOptions = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            };
            
            string updatedJson = JsonSerializer.Serialize(existingLogs, writeOptions);
            await File.WriteAllTextAsync(_evaluationDatasetPath, updatedJson);

            Console.WriteLine($"[RLHF FILE SYSTEM ENGINE] Telemetry successfully committed. Dataset size: {existingLogs.Count} records total.");
        }
        catch (Exception fileEx)
        {
            Console.WriteLine($"[RLHF File Ingestion Error]: Failed to commit training trace: {fileEx.Message}");
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

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
            var response = await _httpClient.PostAsJsonAsync(url, query);
            if (!response.IsSuccessStatusCode) return default;

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            return doc.RootElement.Clone();
        }
        catch
        {
            return default;
        }
    }

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
            var response = await _httpClient.PostAsync(url, new StringContent(JsonSerializer.Serialize(query), Encoding.UTF8, "application/json"));
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

    public async Task<int> GetTotalClassCountAsync(string className)
    {
        var jsonQuery = new { query = $"{{ Aggregate {{ {className} {{ meta {{ count }} }} }} }}" };
        try
        {
            var response = await _httpClient.PostAsJsonAsync("v1/graphql", jsonQuery);
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
            var response = await _httpClient.PostAsJsonAsync("v1/graphql", new { query = gqlQuery });
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

                        if (className == "DocumentLibrary")
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

    public async Task<JsonElement> GetRawInteractionLogsAsync(int limit)
    {
        var jsonQuery = new { query = $"{{ Get {{ InteractionLog(limit: {limit}) {{ query answer was_successful timestamp }} }} }}" };
        try
        {
            var response = await _httpClient.PostAsJsonAsync("v1/graphql", jsonQuery);
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
            var response = await _httpClient.PostAsJsonAsync(url, payload);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Weaviate Telemetry Error] Failed to write active interaction log: {ex.Message}");
        }
    }

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
                var response = await _httpClient.PostAsJsonAsync(url, payload);
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
            var response = await _httpClient.PostAsJsonAsync(url, query);
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
            var response = await _httpClient.PostAsJsonAsync("v1/graphql", requestPayload);
            
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
}