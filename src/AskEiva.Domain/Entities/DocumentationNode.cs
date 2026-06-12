using System;
using System.Collections.Generic;

namespace AskEiva.Domain.Entities;

/// <summary>
/// Represents a structured technical documentation article or reference page captured from an external knowledge distribution stream.
/// </summary>
public class DocumentationNode
{
    /// <summary>
    /// Gets or sets the unique primary tracking identifier associated with the source document.
    /// </summary>
    public string SourceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the descriptive title or headline of the documentation asset.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the specific software product or module category assignment (e.g., "NaviPac", "NaviScan").
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the raw, unsegmented plain-text content body extracted from the source file.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the online uniform resource locator pointing directly to the source web document.
    /// </summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the collection of online resource links pointing to relevant downloadable PDF manuals or reference sheets.
    /// </summary>
    public List<string> AssociatedPdfUrls { get; set; } = new();

    /// <summary>
    /// Gets or sets the universal date and time stamp indicating when the crawler service extracted this documentation.
    /// </summary>
    public DateTime CrawledAt { get; set; } = DateTime.UtcNow;
}