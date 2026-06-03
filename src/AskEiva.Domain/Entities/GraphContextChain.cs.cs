using System;
using System.Collections.Generic;

namespace AskEiva.Domain.Entities;

public class GraphContextChain
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TicketId { get; set; } = string.Empty;              // e.g., "FD-81922"
    public string TicketTitle { get; set; } = string.Empty;           // Capture title for frontend text anchors
    public string MainProductContext { get; set; } = string.Empty;     // e.g., "NaviPac", "NaviEdit"
    public string ScenarioType { get; set; } = string.Empty;           // e.g., "Customer Settings Overrides", "Version Patch Update"
    
    // 💡 THE GRAPHRAG ENGINE: Human & Machine-readable sequence trail
    // Formatted as: (Ticket FD-81922) -[REPORTS_CRASH]-> (NaviPac v4.5) -[CAUSED_BY]-> (Release Notes Chunk 12) -[RESOLVED_BY]-> (Doc Page 4)
    public string SharedPathChain { get; set; } = string.Empty; 
    
    // The explicit list of individual predicates parsed out of the string
    public List<string> Predicates { get; set; } = new();
    
    // 💡 CONTEXT AWARENESS SUMMARY: Written by Mistral, describing how this sequence path 
    // intersects or overlaps with wider scenarios, settings, or customer issues
    public string EnvironmentalContextSummary { get; set; } = string.Empty;
    
    public double ConfidenceScore { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}