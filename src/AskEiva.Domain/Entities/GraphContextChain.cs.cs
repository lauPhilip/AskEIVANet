using System;
using System.Collections.Generic;

namespace AskEiva.Domain.Entities;

/// <summary>
/// Represents a distilled knowledge graph relationship linking support tickets to products, scenarios, and resolution paths.
/// </summary>
public class GraphContextChain
{
    /// <summary>
    /// Gets or sets the unique tracking identifier for this specific graph relationship instance.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the source support ticket identifier (e.g., "FD-81922").
    /// </summary>
    public string TicketId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the descriptive title of the support ticket utilized as text anchors within the interface.
    /// </summary>
    public string TicketTitle { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the specific software product context identified in the relationship loop (e.g., "NaviPac", "NaviEdit").
    /// </summary>
    public string MainProductContext { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the classification category of the interaction scenario (e.g., "Customer Settings Overrides", "Version Patch Update").
    /// </summary>
    public string ScenarioType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the complete, serialized semantic text chain representing the full relationship sequence trail.
    /// </summary>
    /// <remarks>
    /// Example path format: (Ticket FD-81922) -[REPORTS_CRASH]-> (NaviPac v4.5) -[CAUSED_BY]-> (Release Notes Chunk 12) -[RESOLVED_BY]-> (Doc Page 4)
    /// </remarks>
    public string SharedPathChain { get; set; } = string.Empty; 

    /// <summary>
    /// Gets or sets the isolated list of structural verb predicates extracted from the main relationship path.
    /// </summary>
    public List<string> Predicates { get; set; } = new();

    /// <summary>
    /// Gets or sets an AI-generated analytical summary describing how this relationship path intersects with wider environmental settings or customer behaviors.
    /// </summary>
    public string EnvironmentalContextSummary { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the accuracy score reflecting the model confidence calculation for this relationship.
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// Gets or sets the universal date and time stamp indicating exactly when this relationship path was processed.
    /// </summary>
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}