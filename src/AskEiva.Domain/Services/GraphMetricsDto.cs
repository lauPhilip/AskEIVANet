namespace AskEiva.Domain.Services;

public class GraphMetricsDto
{
    public int TotalTriples { get; set; } = 0;
    public int TotalNodes { get; set; } = 0;
    public double SemanticDensity { get; set; } = 0.0;
    public int ConnectedClusters { get; set; } = 0;
    public string PipelineStatus { get; set; } = "Operational";
}