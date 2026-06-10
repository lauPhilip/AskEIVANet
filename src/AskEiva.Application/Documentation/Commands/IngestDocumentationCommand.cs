using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AskEiva.Domain.Repositories;
using AskEiva.Domain.Services; 
using AskEiva.Domain.Utilities;
using AskEiva.Domain.Entities;
using MediatR;

namespace AskEiva.Application.Documentation.Commands;

public record IngestDocumentationCommand(List<string> CategoryIds) : IRequest<DocIngestionResult>;

public record DocIngestionResult(int ArticlesProcessed, int TotalChunksCreated, bool Success, string Message);

public class IngestDocumentationCommandHandler : IRequestHandler<IngestDocumentationCommand, DocIngestionResult>
{
    private readonly IDocumentationCrawler _crawler; 
    private readonly IDocumentationRepository _repository;

    public IngestDocumentationCommandHandler(IDocumentationCrawler crawler, IDocumentationRepository repository)
    {
        _crawler = crawler;
        _repository = repository;
    }

public async Task<DocIngestionResult> Handle(IngestDocumentationCommand request, CancellationToken cancellationToken)
    {
        int articlesCount = 0;
        int totalChunksCount = 0;
        
        var splitter = new TextSplitter(chunkSize: 1000, chunkOverlap: 200); 

        foreach (var categoryId in request.CategoryIds)
        {
            if (cancellationToken.IsCancellationRequested) break;

            // Fetch from crawler stream
            await foreach (var node in _crawler.CrawlSolutionsAsync(categoryId).WithCancellation(cancellationToken))
            {
                Console.WriteLine("\n==================================================");
                Console.WriteLine($"🔍 [DIAGNOSTIC] Processing Article #{articlesCount + 1}");
                Console.WriteLine($"   Title: {node.Title}");
                Console.WriteLine($"   Raw Length: {node.Content?.Length ?? 0} characters");
                Console.WriteLine("==================================================");

                if (string.IsNullOrWhiteSpace(node.Content))
                {
                    Console.WriteLine("   ⚠️ [DIAGNOSTIC WARNING] Content is completely NULL or Empty! Skipping.");
                    articlesCount++;
                    continue;
                }

                // 🎯 CALLING SPLITTER
                var splits = splitter.SplitDocumentation(node).ToList();
                
                Console.WriteLine($"   📊 [DIAGNOSTIC RESULT] Splits Produced: {splits.Count}");
                
                if (splits.Any())
                {
                    foreach (var chunk in splits.Take(2)) // Print the first couple chunks to verify text extraction
                    {
                        Console.WriteLine($"      -> Chunk ID: {chunk.ChunkId}");
                        Console.WriteLine($"         Text Excerpt ({chunk.Content.Length} chars): \"{(chunk.Content.Length > 80 ? chunk.Content.Substring(0, 80) + "..." : chunk.Content)}\"");
                    }
                    if (splits.Count > 2)
                    {
                        Console.WriteLine($"      ... and {splits.Count - 2} more chunks.");
                    }

                    
                    await _repository.BatchIngestDocChunksAsync(splits, documentType: node.Category, globalTags: new() { node.Category, "ManualAsset" });
                    
                    totalChunksCount += splits.Count;
                }
                else
                {
                    Console.WriteLine("   ❌ [DIAGNOSTIC ALERT] Split documentation returned ZERO chunks for this article!");
                }
                
                articlesCount++;
            }
        }

        return new DocIngestionResult(articlesCount, totalChunksCount, true, "Diagnostic run finished.");
    }
}