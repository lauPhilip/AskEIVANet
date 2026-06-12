using System.Collections.Generic;

namespace AskEiva.Domain.ValueObjects;

/// <summary>
/// Represents an unalterable, high-priority semantic search match entry pulled from vector storage index fields.
/// </summary>
/// <param name="SourceId">The unique identification tracking key assigned to the origin document (e.g., "FD-12345").</param>
/// <param name="Title">The descriptive title or subject headline of the matched record entry.</param>
/// <param name="Content">The specific text segment block containing the relevant grounding details.</param>
/// <param name="SourceUrl">The online uniform resource locator link pointing straight back to the original source location.</param>
/// <param name="ConfidenceScore">The calculated similarity weight or mathematical vector distance ranking score.</param>
/// <param name="SourceType">The data archetype grouping classification (e.g., "Ticket", "Documentation", or "ReleaseNote").</param>
/// <param name="ImageUrls">The collection of verified image reference links embedded within this specific text piece.</param>
/// <param name="ProductContext">The related software application context identified for this entry (e.g., "NaviPac").</param>
/// <param name="VersionContext">The specific software release version identifier string if applicable (e.g., "v4.5.2").</param>
public record RetrievalMatch(
    string SourceId,
    string Title,
    string Content,
    string SourceUrl,
    float ConfidenceScore,
    string SourceType,
    List<string> ImageUrls,
    string ProductContext = "", 
    string VersionContext = ""  
);