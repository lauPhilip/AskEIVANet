using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Repositories;
using AskEiva.Domain.ValueObjects;

namespace AskEiva.Infrastructure.Repositories;

/// <summary>
/// Implements the <see cref="IDocumentationRepository"/> contract using an optimized 
/// <see cref="HttpClient"/> pipeline to communicate directly with a Weaviate vector database instance.
/// </summary>
public class DocumentationRepository : IDocumentationRepository
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentationRepository"/> class with a pre-configured HTTP client.
    /// </summary>
    /// <param name="httpClient">An unmanaged or factory-managed HTTP client pointing to the Weaviate REST api base url.</param>
    public DocumentationRepository(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Inserts or updates an unsegmented parent documentation entry record within the Weaviate database instance.
    /// </summary>
    /// <param name="docNode">The domain documentation node entity containing metadata references and raw body content.</param>
    /// <returns>An asynchronous task tracking the execution of the storage transaction.</returns>
    public async Task UpsertDocumentationAsync(DocumentationNode docNode)
    {
        var url = "v1/objects";
        var payload = new
        {
            @class = "DocumentationNode",
            properties = new
            {
                source_id = docNode.SourceId,
                title = docNode.Title,
                category = docNode.Category,
                content = docNode.Content,
                source_url = docNode.SourceUrl,
                pdf_links = docNode.AssociatedPdfUrls,
                crawled_at = docNode.CrawledAt.ToString("o")
            }
        };

        var response = await _httpClient.PostAsJsonAsync(url, payload);
        
        if (!response.IsSuccessStatusCode)
        {
            string err = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[Root Doc Warning]: {response.StatusCode} - {err}");
        }
    }

    /// <summary>
    /// Streams and pushes collection segments of processed text chunk value objects directly 
    /// into Weaviate using its high-performance bulk processing endpoints.
    /// </summary>
    /// <param name="chunks">The sequence of broken, word-bounded text chunk value objects containing localized contents.</param>
    /// <param name="documentType">The categorical grouping classification assigned to the source document asset (e.g., "NaviPac").</param>
    /// <param name="globalTags">The collection of metadata tracking labels applied uniformly across all segment nodes in the batch.</param>
    /// <returns>An asynchronous task tracking the execution of the batch transactions.</returns>
    /// <remarks>
    /// Note: To maximize token threshold constraints and maintain optimal cluster memory consumption, 
    /// chunks are partitioned into sub-packages containing a maximum limit of 40 records per network request.
    /// </remarks>
    public async Task BatchIngestDocChunksAsync(IEnumerable<TextChunk> chunks, string documentType, List<string> globalTags)
    {
        var url = "v1/batch/objects";

        // Map data elements directly onto your new centralized collection schema identity
        var batchObjects = chunks.Select(chunk => new
        {
            @class = "DocumentationLibrary", // Assigned to new correct enhanced target schema
            properties = new
            {
                document_id = chunk.ChunkId,
                title = chunk.Metadata.TryGetValue("Subject", out var subject) ? subject : "Technical Manual Article",
                document_type = documentType,
                content = chunk.Content,
                url = chunk.Metadata.TryGetValue("Url", out var targetUrl) ? targetUrl : "https://eiva.freshdesk.com",
                image_urls = chunk.ImageUrls ?? [],
                tags = globalTags ?? []
            }
        }).ToList();

        // Churn and stream blocks in optimal sub-packages of 40 records to maximize memory-token thresholds
        const int optimalBatchLimit = 40;
        for (int i = 0; i < batchObjects.Count; i += optimalBatchLimit)
        {
            var partitionSubset = batchObjects.Skip(i).Take(optimalBatchLimit).ToList();
            var payloadContainer = new { objects = partitionSubset };

            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, payloadContainer);
                if (!response.IsSuccessStatusCode)
                {
                    string details = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[Weaviate Ingestion Error Segment]: {response.StatusCode} - {details}");
                }
                else
                {
                    Console.WriteLine($"[Weaviate Bulk Engine] Successfully vectorized segment pool ({partitionSubset.Count} nodes) into DocumentationLibrary.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Weaviate Bulk Pipeline Collapse Exception]: {ex.Message}");
            }
        }
    }
}