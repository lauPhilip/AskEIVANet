using System.Collections.Generic;

namespace AskEiva.Domain.ValueObjects;

/// <summary>
/// Represents an unalterable, structural segment of text sliced from a larger asset body, packaged with sequence and routing tracking schemas.
/// </summary>
/// <param name="ChunkId">The unique, composite key tracking this specific slice instance (e.g., "FD-12345_ch_0").</param>
/// <param name="SourceId">The tracking code identifying the source document from which this slice was partitioned.</param>
/// <param name="Content">The isolated, size-bounded block of cleaned text contents.</param>
/// <param name="SequenceNumber">The positional index ordering of this slice within the overall chronological document source layout.</param>
/// <param name="ImageUrls">The collection of images or media asset paths discovered inside this specific section boundary.</param>
/// <param name="Metadata">The collection of flexible key-value attributes containing contextual filters for database searches.</param>
public record TextChunk(
    string ChunkId,
    string SourceId,
    string Content,
    int SequenceNumber,
    List<string> ImageUrls,
    Dictionary<string, string> Metadata
);