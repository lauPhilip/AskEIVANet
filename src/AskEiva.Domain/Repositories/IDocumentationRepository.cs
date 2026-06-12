using System.Collections.Generic;
using System.Threading.Tasks;
using AskEiva.Domain.ValueObjects;

namespace AskEiva.Domain.Repositories;

/// <summary>
/// Defines the storage contract abstractions for persisting, batching, and managing technical documentation assets within the data store.
/// </summary>
public interface IDocumentationRepository
{
    /// <summary>
    /// Inserts or updates a complete, unsegmented technical documentation entry record within the underlying database store.
    /// </summary>
    /// <param name="docNode">The domain documentation node entity containing raw body text and source link metadata references.</param>
    /// <returns>A asynchronous task tracking the completion of the operation.</returns>
    Task UpsertDocumentationAsync(AskEiva.Domain.Entities.DocumentationNode docNode);
    
    /// <summary>
    /// Submits a collection of processed text sub-segments into the vector storage schema in a single batch transaction.
    /// </summary>
    /// <param name="chunks">The sequence of broken, word-bounded text chunk value objects containing localized contents.</param>
    /// <param name="documentType">The categorical grouping classification assigned to the source document asset (e.g., "NaviPac").</param>
    /// <param name="globalTags">The collection of metadata tracking labels applied uniformly across all segment nodes in the batch.</param>
    /// <returns>A asynchronous task tracking the completion of the batch transaction.</returns>
    Task BatchIngestDocChunksAsync(IEnumerable<TextChunk> chunks, string documentType, List<string> globalTags);
}