using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AskEiva.Domain.Entities;

namespace AskEiva.Domain.Repositories;

/// <summary>
/// Defines the storage contract abstractions for writing, searching, and validating structural knowledge graph relationships and semantic text chains.
/// </summary>
public interface IGraphRepository
{
    /// <summary>
    /// Commits a new distilled knowledge relationship chain entity into the graph storage layer.
    /// </summary>
    /// <param name="chain">The structural graph context chain data model containing entity keys, predicates, and confidence metrics.</param>
    /// <returns>An asynchronous task returning true if the record was successfully saved, otherwise false.</returns>
    Task<bool> InsertChainAsync(GraphContextChain chain);

    /// <summary>
    /// Executes a vector proximity lookup to discover relevant knowledge graph paths matching a high-dimensional query vector.
    /// </summary>
    /// <param name="queryVector">The memory block holding the read-only floating-point numerical array of vector values.</param>
    /// <param name="maxResults">The maximum limit number of prioritized graph paths to return.</param>
    /// <returns>A collection of matching graph context chain entities ordered by semantic relevance.</returns>
    Task<List<GraphContextChain>> SearchGraphContextAsync(ReadOnlyMemory<float> queryVector, int maxResults);

    /// <summary>
    /// Retrieves all recorded graph relationship paths linked to a specific customer support ticket identifier.
    /// </summary>
    /// <param name="ticketId">The unique cross-system tracking identifier of the target ticket (e.g., "FD-81922").</param>
    /// <returns>A list containing all structural knowledge paths originating from or linked to the requested ticket node.</returns>
    Task<List<GraphContextChain>> GetChainsByTicketAsync(string ticketId);

    /// <summary>
    /// Verifies if a given customer support ticket has already undergone relationship mapping to ensure pipeline process idempotency.
    /// </summary>
    /// <param name="ticketId">The unique cross-system tracking identifier of the ticket under evaluation.</param>
    /// <returns>An asynchronous task returning true if the ticket already exists in the graph network storage, otherwise false.</returns>
    Task<bool> HasTicketBeenProcessedAsync(string ticketId);
}