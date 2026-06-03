using System.Collections.Generic;
using System.Threading.Tasks;

namespace AskEiva.Domain.Services;

public interface IMistralDistillationService
{
    // 💡 The GraphRAG Compiler Method Contract
    Task<ChainCompilationResponse> CompileContextChainAsync(string ticketId, string ticketBody, string candidatesContext);
    
    // Legacy support fallback pattern string
    Task<DistillationDecision> AnalyzeRelationshipAsync(string ticketContent, string referenceContent, string collectionType);
}

// 💡 FIXED: Declared right here inside the Domain namespace so all projects can see it!
public class ChainCompilationResponse
{
    public bool IsLinked { get; set; } = false;
    public string MainProductContext { get; set; } = string.Empty;
    public string ScenarioType { get; set; } = string.Empty;
    public string SharedPathChain { get; set; } = string.Empty;
    public List<string> Predicates { get; set; } = new();
    public string EnvironmentalContextSummary { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; } = 0.0;
}

public class DistillationDecision
{
    public bool IsLinked { get; set; } = false;
    public string ExtractedPredicate { get; set; } = "RELATED_TO";
    public double ConfidenceScore { get; set; } = 0.0;
}