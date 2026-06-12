using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AskEiva.Domain.Repositories;
using AskEiva.Domain.Services;
using MediatR;

namespace AskEiva.Application.ReleaseNotes.Commands;

/// <summary>
/// MediatR command to trigger the automated web scraping, parsing, and vectorization pipeline for product release notes.
/// </summary>
public record IngestReleaseNotesCommand : IRequest<IngestResultDto>;

/// <summary>
/// Holds the processing metrics and success states resulting from a release notes ingestion run.
/// </summary>
/// <param name="IsSuccess">Indicates if the ingestion process completed without throwing breaking faults.</param>
/// <param name="TotalChunksIngested">The total number of text segments successfully vectorized and stored during the run.</param>
/// <param name="StatusDetails">A plain-text summary detailing the final outcome or error tracking data.</param>
public record IngestResultDto(bool IsSuccess, int TotalChunksIngested, string StatusDetails);

/// <summary>
/// Coordinates the release notes synchronization workflow by crawling external update distributions, 
/// running delta validation steps against existing entries, and batching new records to vector storage.
/// </summary>
public class IngestReleaseNotesCommandHandler : IRequestHandler<IngestReleaseNotesCommand, IngestResultDto>
{
    private readonly IReleaseNotesScraper _scraper;
    private readonly IKnowledgeRetrievalRepository _retrievalRepository;

    /// <summary>
    /// Initializes a new instance of the handler with the necessary scraper utility and target storage interface.
    /// </summary>
    /// <param name="scraper">The external web scraper utility utilized to parse product distribution listings.</param>
    /// <param name="retrievalRepository">The database repository used to check entry states and save vector data nodes.</param>
    public IngestReleaseNotesCommandHandler(IReleaseNotesScraper scraper, IKnowledgeRetrievalRepository retrievalRepository)
    {
        _scraper = scraper;
        _retrievalRepository = retrievalRepository;
    }

    /// <summary>
    /// Handles the release notes ingestion request by invoking web scrapers, filtering out previously processed versions, 
    /// and saving fresh text segments.
    /// </summary>
    /// <param name="request">The incoming command parameters execution object.</param>
    /// <param name="cancellationToken">Token utilized to monitor and safely interrupt background tasks.</param>
    /// <returns>A results data transfer object outlining process counts and verification states.</returns>
    public async Task<IngestResultDto> Handle(IngestReleaseNotesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Trigger the external web scraping logic to collect and slice available release document entries
            var allDiscoveredNodes = await _scraper.ScrapeAndChunkAllReleaseNotesAsync();
            var nodesList = allDiscoveredNodes.ToList();

            if (!nodesList.Any())
            {
                return new IngestResultDto(true, 0, "All available release note documents are up to date. No new records found.");
            }

            // Extract a distinct list of product configurations and versions to verify tracking histories efficiently
            var uniqueGroupedVersions = nodesList
                .Select(n => new { n.Product, n.Version })
                .Distinct()
                .ToList();

            var newNodesToIngest = new System.Collections.Generic.List<AskEiva.Domain.Entities.SoftwareReleaseNode>();

            foreach (var versionGroup in uniqueGroupedVersions)
            {
                if (cancellationToken.IsCancellationRequested) break;

                // Query the vector engine schemas to determine if this product version has already been processed
                bool alreadyIndexed = await _retrievalRepository.DoesProductVersionExistAsync(versionGroup.Product, versionGroup.Version);
                
                if (!alreadyIndexed)
                {
                    // Isolate and add only the missing data nodes to the processing queue
                    var targetChunks = nodesList.Where(n => n.Product == versionGroup.Product && n.Version == versionGroup.Version);
                    newNodesToIngest.AddRange(targetChunks);
                }
            }

            if (!newNodesToIngest.Any())
            {
                return new IngestResultDto(true, 0, "Delta Verification Passed: All crawled versions already exist inside the vector database storage.");
            }

            // Dispatch only the verified fresh data segments into the storage repository
            await _retrievalRepository.BatchIngestReleaseNodesAsync(newNodesToIngest);

            return new IngestResultDto(
                IsSuccess: true,
                TotalChunksIngested: newNodesToIngest.Count,
                StatusDetails: $"Dynamic Sync Completed: Ingested {newNodesToIngest.Count} fresh text nodes across discovered update tracks."
            );
        }
        catch (Exception ex)
        {
            // Return failure status configurations wrapped cleanly for downstream logging or UI notification rendering
            return new IngestResultDto(false, 0, $"Ingestion pipeline failure trace: {ex.Message}");
        }
    }
}