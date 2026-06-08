using System;

namespace AskEiva.Domain.Entities;

public class EvaluationFeedbackLog
{
    public string Id { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string ProposedAnswer { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public string CorrectionNotes { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
    public string GroundTruth { get; set; } = string.Empty; // Actual closing solution from agent replies
    public List<string> Context { get; set; } = new();     // Supporting documentation chunks and release notes
}