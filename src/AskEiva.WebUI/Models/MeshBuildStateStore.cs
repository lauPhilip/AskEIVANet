using System;

namespace AskEiva.WebUI.Models;

public class MeshBuildStateStore
{
    public bool IsProcessing { get; set; } = false;
    public int CurrentCount { get; set; } = 0;
    public int TotalCount { get; set; } = 0;
    public string CurrentTicketId { get; set; } = "None";
    public string StatusMessage { get; set; } = "Idle";

    public int Percentage => TotalCount > 0 ? (int)((double)CurrentCount / TotalCount * 100) : 0;

    public event Action? OnStateChanged;

    public void UpdateProgress(int current, int total, string ticketId, string message)
    {
        CurrentCount = current;
        TotalCount = total;
        CurrentTicketId = ticketId;
        StatusMessage = message;
        OnStateChanged?.Invoke();
    }
}