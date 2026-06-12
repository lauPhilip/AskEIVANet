using System;
using System.Collections.Generic;

namespace AskEiva.Domain.Entities;

/// <summary>
/// Represents an individual segmented text chunk and metadata index derived from a customer support ticket history chain.
/// </summary>
public class TicketNode
{
    /// <summary>
    /// Gets or sets the unique primary cross-system identifier tracking this record (e.g., "FD-12345").
    /// </summary>
    public string SourceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the data collection archetype classification, defaulting to "Ticket".
    /// </summary>
    public string DataType { get; set; } = "Ticket";

    /// <summary>
    /// Gets or sets the original subject headline or issue title of the support interaction.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the specific, isolated text segment block holding conversation history logs.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this ticket node has already been processed by the graph distillation engine.
    /// </summary>
    public bool IsDistilled { get; set; } = false;

    /// <summary>
    /// Gets or sets the online uniform resource locator link pointing straight back to the external system ticket window.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the raw lifecycle status tracking state integer mapping to external CRM enumeration indexes.
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// Gets or sets the urgency priority weight value integer mapping to external CRM enumeration indexes.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets the collection of custom labels, categories, or tracking tags assigned to this ticket record.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Gets or sets the universal date and time stamp indicating exactly when this support ticket was last modified.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}