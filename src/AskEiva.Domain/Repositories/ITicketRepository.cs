using System.Collections.Generic;
using System.Threading.Tasks;
using AskEiva.Domain.Entities;

namespace AskEiva.Domain.Repositories;

/// <summary>
/// Defines the storage contract abstractions for persisting, batching, and managing customer support ticket text chunks and timelines.
/// </summary>
public interface ITicketRepository
{
    /// <summary>
    /// Inserts or updates an individual support ticket data segment record within the underlying data store.
    /// </summary>
    /// <param name="ticket">The ticket node data model instance holding conversation text slices and CRM metadata.</param>
    /// <returns>An asynchronous task tracking the completion of the operation.</returns>
    Task UpsertTicketAsync(TicketNode ticket);

    /// <summary>
    /// Retrieves a subset of raw ticket nodes that have not yet been evaluated by the relationship distillation systems.
    /// </summary>
    /// <param name="limit">The maximum number of unprocessed ticket segments to fetch.</param>
    /// <returns>A collection of unprocessed ticket nodes containing conversation texts.</returns>
    Task<IEnumerable<TicketNode>> GetUnprocessedTicketsAsync(int limit);

    /// <summary>
    /// Updates the distillation tracking state of a ticket, indicating its relationship paths have been mapped to the knowledge graph.
    /// </summary>
    /// <param name="sourceId">The unique tracking identifier of the completed target ticket node (e.g., "FD-12345").</param>
    /// <returns>An asynchronous task tracking the completion of the operation.</returns>
    Task MarkAsDistilledAsync(string sourceId);

    /// <summary>
    /// Verifies if a given support ticket tracking identifier already exists inside the local database indexes to avoid duplicate ingestions.
    /// </summary>
    /// <param name="sourceId">The unique cross-system identifier tracking the ticket entry.</param>
    /// <returns>True if the record matches an existing index in storage, otherwise false.</returns>
    Task<bool> DoesTicketExistAsync(string sourceId);

    /// <summary>
    /// Submits a collection of processed support ticket text segments into the database schema in a single batch transaction.
    /// </summary>
    /// <param name="tickets">The sequence of ticket node entities containing conversation history segments.</param>
    /// <returns>An asynchronous task tracking the completion of the batch transaction.</returns>
    Task BatchIngestTicketNodesAsync(IEnumerable<TicketNode> tickets); 

    /// <summary>
    /// Fetches a distinct list of core ticket header definitions needing relationship graph distillation passes, bypassing bulk text weights.
    /// </summary>
    /// <param name="batchSize">The maximum number of ticket tracking headers to load into the execution block.</param>
    /// <returns>A list of ticket nodes populated with essential identification keys and header fields.</returns>
    Task<List<TicketNode>> GetUnprocessedTicketHeadersAsync(int batchSize);

    /// <summary>
    /// Compiles all isolated text fragments and reply loops matching a specific ticket identifier into a single stitched timeline string.
    /// </summary>
    /// <param name="sourceId">The unique cross-system tracking identifier of the target ticket node.</param>
    /// <returns>A continuous, chronological plain-text string containing the complete conversation timeline history.</returns>
    Task<string> GetStitchedTicketContentAsync(string sourceId);
}