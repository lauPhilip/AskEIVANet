using System;

namespace AskEiva.Application.Telemetry;

/// <summary>
/// Defines a centralized message broker contract allowing components to subscribe to and publish 
/// real-time data synchronization progress updates across the system.
/// </summary>
public interface ISyncTelemetryBroker
{
    /// <summary>
    /// Event fired whenever a new data synchronization progress record is published through the broker.
    /// </summary>
    event Action<SyncProgressUpdate>? OnProgressUpdated;

    /// <summary>
    /// Broadcasts an updated synchronization progress state to all active subscribers.
    /// </summary>
    /// <param name="update">The progress update containing real-time status and telemetry logs.</param>
    void Broadcast(SyncProgressUpdate update);
}

/// <summary>
/// Provides an in-memory messenger implementation to route real-time telemetry metrics 
/// from background synchronization handlers up to active user interface dashboards.
/// </summary>
public class SyncTelemetryBroker : ISyncTelemetryBroker
{
    /// <summary>
    /// Event fired whenever a new data synchronization progress record is published through the broker.
    /// </summary>
    public event Action<SyncProgressUpdate>? OnProgressUpdated;

    /// <summary>
    /// Distributes progress metrics to all active component subscribers.
    /// </summary>
    /// <param name="update">The progress data model containing tracking counts and status metrics.</param>
    public void Broadcast(SyncProgressUpdate update)
    {
        // Safe invocation check ensures the event triggers only if there are active subscribers listening
        OnProgressUpdated?.Invoke(update);
    }
}

/// <summary>
/// Holds real-time progress update parameters and diagnostic message states for data ingestion tracking.
/// </summary>
public class SyncProgressUpdate
{
    /// <summary>
    /// Gets or sets the descriptive plain-text trace message or log entry for the current operation.
    /// </summary>
    public string LogMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the active index page number processed by the ongoing ingestion loop.
    /// </summary>
    public int CurrentPage { get; set; }

    /// <summary>
    /// Gets or sets the unique tracking identifier of the record currently undergoing synchronization.
    /// </summary>
    public string CurrentTicketId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the summary headline or subject line of the record currently undergoing synchronization.
    /// </summary>
    public string TicketSubject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of split text chunks generated from the current record content block.
    /// </summary>
    public int ChunksGenerated { get; set; }

    /// <summary>
    /// Gets or sets the total running count of text segments successfully committed to the database during this session run.
    /// </summary>
    public int TotalChunksIndexed { get; set; }

    /// <summary>
    /// Gets or sets the processing lifecycle status text (e.g., "Processing", "Success", "Skip", "Complete").
    /// </summary>
    public string Status { get; set; } = "Processing";
}