using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using AskEiva.Domain.Services;

namespace AskEiva.Infrastructure.Services;

/// <summary>
/// Implements the <see cref="IMistralDistillationService"/> contract using an structured JSON 
/// HTTP inference pipeline to call Mistral AI engines, compiling unlinked records into GraphRAG context chains.
/// </summary>
public class MistralDistillationService : IMistralDistillationService
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="MistralDistillationService"/> class with an explicit HTTP client.
    /// </summary>
    /// <param name="httpClient">An unmanaged or factory-allocated HTTP client containing Mistral base URL routing and authorization keys.</param>
    public MistralDistillationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Analyzes a support ticket text string against reference candidates using structured JSON instruction templates 
    /// to engineer multi-hop context paths ranging from 3 to 15 semantic edges deep.
    /// </summary>
    /// <param name="ticketId">The unique alphanumeric tracking key code of the anchor customer ticket.</param>
    /// <param name="ticketBody">The stitched continuous text dialogue representation of the support ticket lifecycle thread.</param>
    /// <param name="candidatesContext">The candidate technical manuals or software release logs cross-referenced for linkage.</param>
    /// <returns>A fully parsed, strongly typed <see cref="ChainCompilationResponse"/> entity containing relational predicates.</returns>
    public async Task<ChainCompilationResponse> CompileContextChainAsync(string ticketId, string ticketBody, string candidatesContext)
    {
        try
        {
            // System prompt rewritten to empower Mistral to engineer 3-15 element scenario strings
            var systemPrompt = """
            You are an advanced GraphRAG Core Compiler for EIVA marine engineering software tracking.
            Your task is to analyze a support ticket alongside matching documentation AND release logs to engineer a context-aware sequence path chain.

            You are completely unrestricted by simple triplets. You can build multi-node semantic paths anywhere from 3 to 15 hops deep based on what you observe.
            Identify specific product scenarios (e.g. NaviPac customer troubleshooting, NaviPac software update regressions, custom settings overrides).

            You must output a raw, valid JSON object matching this structure exactly:
            {
              "isLinked": true,
              "mainProductContext": "NaviPac",
              "scenarioType": "Customer Troubleshooting",
              "sharedPathChain": "(Ticket FD-82022) -[REPORTS_CRASH]-> (NaviPac v4.5) -[CAUSED_BY]-> (Release Note Chunk 12) -[RESOLVED_BY]-> (Setup steps on Doc Page 4)",
              "predicates": ["REPORTS_CRASH", "CAUSED_BY", "RESOLVED_BY"],
              "environmentalContextSummary": "Detail how this sequence sequence intersects or overlaps with wider environmental updates, settings, or multi-instance systems.",
              "confidenceScore": 0.95
            }
            Do not include markdown triple-ticks (```) or extra conversational prose. Respond ONLY with valid JSON.
            """;

            var userPrompt = $"""
            [Source Freshdesk Ticket ID: {ticketId}]
            {ticketBody}

            [Cross-Collection Infrastructure Search Candidates (Docs & Releases)]:
            {candidatesContext}
            """;

            // Using the recommended replacement endpoint for our stack configuration
            var payload = new
            {
                model = "mistral-medium-3-5",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                response_format = new { type = "json_object" },
                temperature = 0.15
            };

            var response = await _httpClient.PostAsJsonAsync("v1/chat/completions", payload);
            if (!response.IsSuccessStatusCode) return new ChainCompilationResponse();

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var choice = doc.RootElement.GetProperty("choices")[0].GetProperty("message");
            string? jsonResponse = choice.GetProperty("content").GetString();

            if (!string.IsNullOrEmpty(jsonResponse))
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<ChainCompilationResponse>(jsonResponse, options) ?? new ChainCompilationResponse();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Mistral Service Chain Error]: {ex.Message}");
        }

        return new ChainCompilationResponse { IsLinked = false };
    }

    /// <summary>
    /// Executes a binary correlation analysis fallback method between an isolated ticket item and a single text element.
    /// </summary>
    /// <param name="ticketContent">The core text content node matching the support log row context parameters.</param>
    /// <param name="referenceContent">The single technical reference item text content block under review.</param>
    /// <param name="collectionType">The collection partition archetype name assigned to the reference chunk asset.</param>
    /// <returns>A structured <see cref="DistillationDecision"/> payload detailing connectivity status and edge values.</returns>
    /// <remarks>
    /// Note: This method serves as a legacy adapter layout block to satisfy older service signatures while utilizing 
    /// the upgraded multi-hop graph compiler infrastructure underneath.
    /// </remarks>
    public async Task<DistillationDecision> AnalyzeRelationshipAsync(string ticketContent, string referenceContent, string collectionType)
    {
        var result = await CompileContextChainAsync("Legacy-ID", ticketContent, referenceContent);
        return new DistillationDecision 
        { 
            IsLinked = result.IsLinked, 
            ExtractedPredicate = result.ScenarioType, 
            ConfidenceScore = result.ConfidenceScore 
        };
    }
}