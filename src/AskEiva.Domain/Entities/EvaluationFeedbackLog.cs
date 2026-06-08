using System;

namespace AskEiva.Domain.Entities;

public class EvaluationFeedbackLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Query { get; set; } = string.Empty;
    public string ProposedAnswer { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public string CorrectionNotes { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty; // e.g., "ClosedBaseline"
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
}