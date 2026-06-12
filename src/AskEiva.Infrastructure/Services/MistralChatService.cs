using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AskEiva.Domain.Services;
using AskEiva.Domain.ValueObjects;
using AskEiva.Domain.Entities;

namespace AskEiva.Infrastructure.Services;

/// <summary>
/// Implements the <see cref="IMistralChatService"/> contract using a highly efficient, token-by-token 
/// Server-Sent Events (SSE) streaming pipeline to dispatch contextual prompts to Mistral AI endpoints.
/// </summary>
public class MistralChatService : IMistralChatService
{
    private readonly HttpClient _httpClient;
    private const string MistralModel = "mistral-large-latest";

    /// <summary>
    /// Initializes a new instance of the <see cref="MistralChatService"/> class with a pre-configured HTTP client factory.
    /// </summary>
    /// <param name="httpClient">An unmanaged or factory-allocated HTTP client injected with system connection headers and API keys.</param>
    public MistralChatService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Compiles standard semantic text blocks alongside structural multi-hop graph context paths and back-and-forth 
    /// session records into a unified reasoning prompt, yielding individual token words in real time.
    /// </summary>
    /// <param name="userQuestion">The incoming, unresolved natural language question from the maritime support operator.</param>
    /// <param name="semanticContext">A sequence of text fragments retrieved from standard vector search lookups.</param>
    /// <param name="structuralGraphMashes">A collection of multi-hop causal relation paths linking historical workspaces together.</param>
    /// <param name="conversationHistory">The back-and-forth interaction history logs within the active user session.</param>
    /// <returns>An asynchronous stream yielding real-time text fragments from the language model engine.</returns>
    public async IAsyncEnumerable<string> GenerateStreamingChatResponseAsync(
        string userQuestion, 
        IEnumerable<RetrievalMatch> semanticContext, 
        IEnumerable<GraphContextChain> structuralGraphMashes,
        IEnumerable<ChatTurn> conversationHistory)
    {
        // 1. Compile standard text fragments
        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine("=== RETRIEVED REFERENCE CONTEXT ===");
        foreach (var match in semanticContext)
        {
            contextBuilder.AppendLine($"[{match.SourceType}] Title: {match.Title}\nContent: {match.Content}\n---");
        }

        // 2. Inject distilled multi-hop graph path relationships seamlessly
        if (structuralGraphMashes != null && structuralGraphMashes.Any())
        {
            contextBuilder.AppendLine("\n=== RETRIEVED HISTORICAL WORKSPACE GRAPH-MESH SCENARIOS ===");
            foreach (var mesh in structuralGraphMashes)
            {
                contextBuilder.AppendLine($"[Support Reference Path: #{mesh.TicketId} | System Context: {mesh.MainProductContext} ({mesh.ScenarioType})]");
                contextBuilder.AppendLine($"Environment Conditions: {mesh.EnvironmentalContextSummary}");
                contextBuilder.AppendLine($"Causal Knowledge Path Traversal Chain: {mesh.SharedPathChain}");
                contextBuilder.AppendLine($"Graph Verification Confidence: {mesh.ConfidenceScore}%");
                contextBuilder.AppendLine("---");
            }
        }

        var messagesList = new List<object>
        {
            new { 
                role = "system", 
                content = "You are AskEIVA, an expert automated customer support engineer. Synthesize the provided support tickets, technical documentation, release manifests, and historical knowledge graph context paths to answer questions. Format outputs using clear Markdown syntax." 
            }
        };

        foreach (var turn in conversationHistory)
        {
            messagesList.Add(new { 
                role = turn.IsUser ? "user" : "assistant", 
                content = turn.MessageText 
            });
        }

        messagesList.Add(new { 
            role = "user", 
            content = $"Context Data Elements:\n{contextBuilder}\n\nNew User Question: {userQuestion}" 
        });

        var payload = new
        {
            model = MistralModel,
            messages = messagesList.ToArray(),
            temperature = 0.2,
            stream = true
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        // Enforce ResponseHeadersRead to process server-sent chunks without caching the entire stream inside application buffers
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            yield return $"Mistral Stream Failure: {response.StatusCode}";
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

            var data = line[6..].Trim();
            if (data == "[DONE]") break;

            MistralStreamChunk? chunk = null;
            try { chunk = JsonSerializer.Deserialize<MistralStreamChunk>(data); } catch { continue; }

            var textChunk = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
            if (!string.IsNullOrEmpty(textChunk))
            {
                yield return textChunk;
            }
        }
    }
}

// --- STREAMING-SPECIFIC DESERIALIZATION SCHEMAS ---

/// <summary>
/// Root data wrapper structure for incoming JSON data packets during Server-Sent Events (SSE) chat streams.
/// </summary>
public class MistralStreamChunk
{
    /// <summary>
    /// Gets or sets the individual structural layout options associated with this streaming segment slice.
    /// </summary>
    [JsonPropertyName("choices")] public List<MistralStreamChoice>? Choices { get; set; }
}

/// <summary>
/// Intermediate data model capturing sequential delta evaluation steps inside incoming model stream lists.
/// </summary>
public class MistralStreamChoice
{
    /// <summary>
    /// Gets or sets the localized text delta payload token sent inside the server stream.
    /// </summary>
    [JsonPropertyName("delta")] public MistralStreamDelta? Delta { get; set; }
}

/// <summary>
/// Pinpoints the inner raw data text content slice calculated by the language model during this sequence pass.
/// </summary>
public class MistralStreamDelta
{
    /// <summary>
    /// Gets or sets the partial text content slice tracking string value.
    /// </summary>
    [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
}