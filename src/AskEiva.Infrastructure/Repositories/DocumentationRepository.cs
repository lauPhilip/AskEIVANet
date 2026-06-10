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

public class DocumentationRepository : IDocumentationRepository
{
    private readonly HttpClient _httpClient;

    public DocumentationRepository(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

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
        // Ensure success or gracefully log if class doesn't exist yet
        if (!response.IsSuccessStatusCode)
        {
            string err = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[Root Doc Warning]: {response.StatusCode} - {err}");
        }
    }

    // 💡 THE CHUNKING VECTORIZER ENGINE: Batches data straight into Weaviate cluster collections
// 💡 UPGRADED BULK POSTING ENGINE: Streams data matrices using Weaviate's high-performance batch endpoints
    public async Task BatchIngestDocChunksAsync(IEnumerable<TextChunk> chunks, string documentType, List<string> globalTags)
    {
        var url = "v1/batch/objects";

        // Map data elements directly onto your new centralized collection schema identity
        var batchObjects = chunks.Select(chunk => new
        {
            @class = "DocumentationLibrary", // 💡 ASSIGNED TO NEW CORRECT ENHANCED TARGET
            properties = new
            {
                document_id = chunk.ChunkId,
                title = chunk.Metadata.ContainsKey("Subject") ? chunk.Metadata["Subject"] : "Technical Manual Article",
                document_type = documentType,
                content = chunk.Content,
                url = chunk.Metadata.ContainsKey("Url") ? chunk.Metadata["Url"] : "https://eiva.freshdesk.com",
                image_urls = chunk.ImageUrls ?? new List<string>(),
                tags = globalTags ?? new List<string>()
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