using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Repositories;
using AskEiva.Domain.Services;
using AskEiva.Application.Telemetry; 

namespace AskEiva.Application.Tickets.Commands;

/// <summary>
/// MediatR command to initiate paginated retrieval, sanitization, and vector indexing for historical customer support tickets.
/// </summary>
public record IngestTicketsCommand : IRequest<int>;

/// <summary>
/// Coordinates the end-to-end Freshdesk ticket synchronization workflow by fetching historical conversations, 
/// cleaning inline media streams, text chunking data strings, and committing batch records to database storage layers.
/// </summary>
public class IngestTicketsCommandHandler : IRequestHandler<IngestTicketsCommand, int>
{
    private readonly IFreshdeskService _freshdeskService;
    private readonly ITicketRepository _ticketRepository;
    private readonly ISyncTelemetryBroker _telemetryBroker; 

    /// <summary>
    /// Initializes a new instance of the handler with Freshdesk data streams, database abstractions, and telemetry event channels.
    /// </summary>
    /// <param name="freshdeskService">External service interface interacting with Freshdesk data endpoints.</param>
    /// <param name="ticketRepository">Data repository handling local ticket storage states and lookups.</param>
    /// <param name="telemetryBroker">Real-time messenger hub used to send status metrics back to UI components.</param>
    public IngestTicketsCommandHandler(IFreshdeskService freshdeskService, ITicketRepository ticketRepository, ISyncTelemetryBroker telemetryBroker)
    {
        _freshdeskService = freshdeskService;
        _ticketRepository = ticketRepository;
        _telemetryBroker = telemetryBroker;
    }

    /// <summary>
    /// Executes the primary paginated retrieval loop, filtering duplicate database entries, rebuilding conversational timelines, and executing batch index inserts.
    /// </summary>
    /// <param name="request">The incoming execution instruction parameter wrapper object.</param>
    /// <param name="cancellationToken">Token utilized to monitor and safely interrupt background tasks.</param>
    /// <returns>The total number of individual text sub-chunks successfully committed to database storage layers.</returns>
    public async Task<int> Handle(IngestTicketsCommand request, CancellationToken cancellationToken)
    {
        int currentPage = 1;
        const int TicketsPerPage = 30; 
        int totalNewChunksIndexed = 0;
        bool crawling = true;

        _telemetryBroker.Broadcast(new SyncProgressUpdate 
        { 
            LogMessage = "🚀 Global Archive Sweep Initialized. Using updated_since chronological filters to bypass default view caps...", 
            Status = "Processing" 
        });

        while (crawling)
        {
            if (cancellationToken.IsCancellationRequested) break;

            _telemetryBroker.Broadcast(new SyncProgressUpdate 
            { 
                LogMessage = $"📡 Requesting page index sequential layer: [Page {currentPage}]", 
                CurrentPage = currentPage 
            });
            
            // Request a single chunked page of ticket arrays from the API service provider
            var ticketBatch = await _freshdeskService.GetTicketsPageAsync(currentPage, TicketsPerPage);
            var batchList = ticketBatch.ToList();

            // If the current index layer page contains zero records, the pipeline has reached the end of available history
            if (!batchList.Any())
            {
                _telemetryBroker.Broadcast(new SyncProgressUpdate 
                { 
                    LogMessage = $"✅ Ingestion caught up! Completed scan at Page {currentPage - 1}. All data records verified.", 
                    Status = "Complete" 
                });
                break;
            }

            var chunksToIngest = new List<TicketNode>();

            foreach (var ticket in batchList)
            {
                if (cancellationToken.IsCancellationRequested) break;
                string sourceIdToken = $"FD-{ticket.Id}";

                // Skip processing tasks if the unique source identifier matches an existing record in the database
                if (await _ticketRepository.DoesTicketExistAsync(sourceIdToken))
                {
                    continue; 
                }

                if (string.IsNullOrWhiteSpace(ticket.Description_Text) || ticket.Description_Text.Length < 30) continue;

                // Sanitize raw text bodies by scrubbing attachment indicators and blank lines
                string cleanedMainText = SanitizeTicketPayloadBody(ticket.Description_Text);
                var timelineBuilder = new System.Text.StringBuilder();
                timelineBuilder.AppendLine($"=== TICKET OPENED: {ticket.Subject} ===");
                timelineBuilder.AppendLine(cleanedMainText);

                // Fetch conversational thread messages associated with the current ticket item
                var subReplies = await _freshdeskService.GetTicketConversationsAsync(ticket.Id);
                foreach (var reply in subReplies.OrderBy(r => r.Created_At))
                {
                    string actorRole = reply.Incoming ? "CUSTOMER" : "AGENT";
                    string cleanedReplyText = SanitizeTicketPayloadBody(reply.Body_Text);
                    if (string.IsNullOrWhiteSpace(cleanedReplyText) || cleanedReplyText.Length < 10) continue;

                    timelineBuilder.AppendLine($"\n--- Reply by {actorRole} on {reply.Created_At} ---");
                    timelineBuilder.AppendLine(cleanedReplyText);
                }

                // Slice the continuous text timeline into word-bounded segments to align with vector capacity ceilings
                var textSegments = SliceBodyIntoChunks(timelineBuilder.ToString(), maxWords: 420);
                int segmentPart = 1;
                
                foreach (var segment in textSegments)
                {
                    var enrichedTags = new List<string> { $"Part-{segmentPart}", $"Year-{ticket.Created_At.Year}" };
                    if (ticket.Tags != null) enrichedTags.AddRange(ticket.Tags);

                    // Hydrate domain data models with unified metadata keys and chunk contents
                    chunksToIngest.Add(new TicketNode
                    {
                        SourceId = sourceIdToken,
                        Subject = ticket.Subject,
                        Content = $"[Historical Support Log Asset | Token ID: {sourceIdToken}]\n{segment}",
                        Url = $"https://eiva.freshdesk.com/a/tickets/{ticket.Id}",
                        DataType = "Ticket",
                        IsDistilled = false,
                        Status = ticket.Status,
                        Priority = ticket.Priority,
                        Tags = enrichedTags
                    });
                    segmentPart++;
                }

                _telemetryBroker.Broadcast(new SyncProgressUpdate 
                { 
                    LogMessage = $"📥 Parsed support thread {sourceIdToken} into vector layers.",
                    CurrentTicketId = sourceIdToken,
                    TicketSubject = ticket.Subject,
                    Status = "Processing"
                });
            }

            if (chunksToIngest.Any())
            {
                // Push the parsed domain chunks down into database repositories
                await _ticketRepository.BatchIngestTicketNodesAsync(chunksToIngest);
                totalNewChunksIndexed += chunksToIngest.Count;
                
                _telemetryBroker.Broadcast(new SyncProgressUpdate 
                { 
                    LogMessage = $"📦 Vectorized page {currentPage} committed. New chunks: {totalNewChunksIndexed}",
                    TotalChunksIndexed = totalNewChunksIndexed,
                    Status = "Success" 
                });
            }

            currentPage++;
            
            // Introduce a standard delay interval loop to maintain safe compliance with provider API throttling ceilings
            await Task.Delay(1500, cancellationToken); 
        }

        return totalNewChunksIndexed;
    }

    /// <summary>
    /// Uses targeted regex tracking rules to clean raw body inputs by removing large byte payloads, links, or repeating line breaks.
    /// </summary>
    /// <param name="rawText">The raw plain-text string extracted from the ticketing API.</param>
    /// <returns>A clean, uniform string with unnecessary media and spacing information stripped out.</returns>
    private string SanitizeTicketPayloadBody(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;
        
        // Remove structural inline attachment indicators
        string result = Regex.Replace(rawText, @"\[inline_attachment:[^\]]*\]", " [Attachment Removed] ", RegexOptions.IgnoreCase);
        
        // Strip image urls to clear text structures
        result = Regex.Replace(result, @"http[s]?://[^\s]*\.(png|jpg|jpeg|gif)", " [Image URI Stripped] ", RegexOptions.IgnoreCase);
        
        // Suppress long base64 inline binary strings from clogging embedding matrices
        result = Regex.Replace(result, @"(I:[A-Za-z0-9+/]{4}){10,}(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?", " [Raw Binary Stream Suppressed] ");
        
        // Standardize newline formatting characters across diverse platform formats
        result = Regex.Replace(result, @"\r\n?|\n", "\n");
        result = Regex.Replace(result, @"\n{3,}", "\n\n");
        
        return result.Trim();
    }

    /// <summary>
    /// Slices a high-length string into multiple sub-segments using word-boundary index spaces.
    /// </summary>
    /// <param name="rawText">The combined historical text string content block.</param>
    /// <param name="maxWords">The max word target allowed inside any singular text slice block.</param>
    /// <returns>A collection of isolated string segments.</returns>
    private List<string> SliceBodyIntoChunks(string rawText, int maxWords)
    {
        var chunks = new List<string>();
        var words = rawText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i += maxWords)
        {
            chunks.Add(string.Join(" ", words.Skip(i).Take(maxWords)));
        }
        return chunks;
    }
}