using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AskEiva.Domain.Repositories;
using AskEiva.Domain.Services;
using MediatR;

namespace AskEiva.Application.ReleaseNotes.Commands;

/// <summary>
/// Domain-driven MediatR intent command to trigger the end-to-end automated scraping,
/// parsing, chunking, and database ingestion pipeline for EIVA Product Release Notes.
/// </summary>
public record IngestReleaseNotesCommand : IRequest<IngestResultDto>;

public record IngestResultDto(bool IsSuccess, int TotalChunksIngested, string StatusDetails);

public class IngestReleaseNotesCommandHandler : IRequestHandler<IngestReleaseNotesCommand, IngestResultDto>
{
    private readonly IReleaseNotesScraper _scraper;
    private readonly IKnowledgeRetrievalRepository _retrievalRepository;

    public IngestReleaseNotesCommandHandler(IReleaseNotesScraper scraper, IKnowledgeRetrievalRepository retrievalRepository)
    {
        _scraper = scraper;
        _retrievalRepository = retrievalRepository;
    }

    public async Task<IngestResultDto> Handle(IngestReleaseNotesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Trigger the unmocked live infrastructure scraping loop
            var liveExtractedNodes = await _scraper.ScrapeAndChunkAllReleaseNotesAsync();
            var nodesList = liveExtractedNodes.ToList();

            if (!nodesList.Any())
            {
                return new IngestResultDto(false, 0, "Scraping cycle completed but no new release documents or updates were discovered.");
            }

            // 2. Asynchronously stream the transaction batch directly up to Weaviate vector pools
            await _retrievalRepository.BatchIngestReleaseNodesAsync(nodesList);

            return new IngestResultDto(
                IsSuccess: true, 
                TotalChunksIngested: nodesList.Count, 
                StatusDetails: $"Successfully crawled, split, and committed {nodesList.Count} high-fidelity release note blocks to the SoftwareReleaseNode collection."
            );
        }
        catch (Exception ex)
        {
            return new IngestResultDto(false, 0, $"Ingestion pipeline fault: {ex.Message}");
        }
    }
}