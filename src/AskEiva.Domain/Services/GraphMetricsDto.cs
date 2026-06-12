namespace AskEiva.Domain.Services;

/// <summary>
/// Holds calculated network topology metrics and system status states describing the health and structural density of the knowledge graph.
/// </summary>
public class GraphMetricsDto
{
    /// <summary>
    /// Gets or sets the total count of individual semantic triple relationships stored within the graph network.
    /// </summary>
    public int TotalTriples { get; set; } = 0;

    /// <summary>
    /// Gets or sets the total count of unique entity nodes present across the entire network canvas.
    /// </summary>
    public int TotalNodes { get; set; } = 0;

    /// <summary>
    /// Gets or sets the structural density ratio metric indicating how interconnected the available nodes are.
    /// </summary>
    public double SemanticDensity { get; set; } = 0.0;

    /// <summary>
    /// Gets or sets the total number of isolated scenario cluster groupings or conversational hubs detected within the layout topology.
    /// </summary>
    public int ConnectedClusters { get; set; } = 0;

    /// <summary>
    /// Gets or sets the current functional state descriptor string of the graph processing architecture (e.g., "Operational", "Rebuilding").
    /// </summary>
    public string PipelineStatus { get; set; } = "Operational";
}