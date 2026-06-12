using System;

namespace AskEiva.Domain.Entities;

/// <summary>
/// Represents a segmented text chunk and metadata profile extracted from a corporate software release note or product update distribution.
/// </summary>
public class SoftwareReleaseNode
{
    /// <summary>
    /// Gets or sets the unique primary identifier tracking this specific release note chunk instance.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the higher-level organizational group classification grouping this document type.
    /// </summary>
    public string GroupCategory { get; set; } = string.Empty;     

    /// <summary>
    /// Gets or sets the target software application name (e.g., "NaviPac", "NaviScan").
    /// </summary>
    public string Product { get; set; } = string.Empty;           

    /// <summary>
    /// Gets or sets the distribution deployment variant classification (e.g., "Major Release", "Hotfix", "Patch").
    /// </summary>
    public string ReleaseType { get; set; } = string.Empty;     

    /// <summary>
    /// Gets or sets the literal numerical version string associated with this update asset (e.g., "4.5.2").
    /// </summary>
    public string Version { get; set; } = string.Empty;           

    /// <summary>
    /// Gets or sets the complete, descriptive identifier combining product naming and version text for frontend queries.
    /// </summary>
    public string FullVersionTitle { get; set; } = string.Empty;  

    /// <summary>
    /// Gets or sets optional administrative metadata descriptions, internal tracking notes, or file comments.
    /// </summary>
    public string MetadataNote { get; set; } = string.Empty;   

    /// <summary>
    /// Gets or sets the date and time when this software update track was officially made available.
    /// </summary>
    public DateTime ReleaseDate { get; set; }

    /// <summary>
    /// Gets or sets the specific header title or text label of the document section from which this chunk was sliced.
    /// </summary>
    public string SectionHeader { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the isolated, text content block containing the actual feature updates, modifications, or bug fix logs.
    /// </summary>
    public string ContentChunk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets cross-reference strings mapping this update to external customer or system ticket identifiers.
    /// </summary>
    public string RefTickets { get; set; } = string.Empty;
}