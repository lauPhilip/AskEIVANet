using System;

namespace AskEiva.Domain.Entities;

public class SoftwareReleaseNode
{
    public string Id { get; set; } = string.Empty;
    public string Product { get; set; } = "NaviPac"; // Default product classification
    public string Version { get; set; } = string.Empty;
    public DateTime ReleaseDate { get; set; }
    public string SectionHeader { get; set; } = string.Empty; // e.g., "Kernel", "Helmsman 4.13", or "GeoCalc"
    public string ContentChunk { get; set; } = string.Empty;  // Raw bullet points or textual descriptions
    public string RefTickets { get; set; } = string.Empty;    // Associated Jira / Freshdesk IDs
}