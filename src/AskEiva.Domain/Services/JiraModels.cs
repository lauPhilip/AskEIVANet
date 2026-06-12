using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AskEiva.Domain.Services;

/// <summary>
/// Represents the root serialization wrapper model returned by Atlassian Jira search API endpoints.
/// </summary>
public class JiraSearchResponse
{
    /// <summary>
    /// Gets or sets the collection layout array containing raw target issue records.
    /// </summary>
    [JsonPropertyName("issues")]
    public List<JiraIssueRawDto> Issues { get; set; } = new();

    /// <summary>
    /// Gets or sets the total running tally count of matching issues registered inside the project server.
    /// </summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }
}

/// <summary>
/// Maps the root identifying tokens and structural fields for a specific issue item from the raw API JSON.
/// </summary>
public class JiraIssueRawDto
{
    /// <summary>
    /// Gets or sets the visible corporate tracking key of the ticket (e.g., "NAV-4201").
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the system-generated unique numerical tracking key of the issue entity.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the nested sub-properties and detail fields associated with this issue.
    /// </summary>
    [JsonPropertyName("fields")]
    public JiraFieldsRawDto Fields { get; set; } = new();
}

/// <summary>
/// Models the standard core collection fields and nested metadata structures mapped out of a standard Jira issue.
/// </summary>
public class JiraFieldsRawDto
{
    /// <summary>
    /// Gets or sets the summary headline or title text string of the tracked task asset.
    /// </summary>
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the raw description object metadata payload.
    /// </summary>
    /// <remarks>
    /// Note: Typed intentionally as a generic object to safely absorb Jira's highly nested 
    /// Atlassian Document Format (ADF) JSON graph structures without throwing parser exceptions.
    /// </remarks>
    [JsonPropertyName("description")]
    public object? Description { get; set; }

    /// <summary>
    /// Gets or sets the origin project category identification fields assigned to the issue.
    /// </summary>
    [JsonPropertyName("project")]
    public JiraProjectDto? Project { get; set; }

    /// <summary>
    /// Gets or sets the task variant category categorization (e.g., "Bug", "Story", "Task").
    /// </summary>
    [JsonPropertyName("issuetype")]
    public JiraIssueTypeDto? IssueType { get; set; }

    /// <summary>
    /// Gets or sets the active pipeline layout state metric metadata fields (e.g., "In Progress", "Resolved").
    /// </summary>
    [JsonPropertyName("status")]
    public JiraStatusDto? Status { get; set; }

    /// <summary>
    /// Gets or sets the container holding individual historical user comments linked to this record card.
    /// </summary>
    [JsonPropertyName("comment")]
    public JiraCommentCollectionDto? CommentCollection { get; set; }
}

/// <summary>
/// Maps identifying parameters describing the parent Jira project framework container.
/// </summary>
public class JiraProjectDto
{
    /// <summary>
    /// Gets or sets the short code abbreviation tracking sequence character key of the project (e.g., "NAVIPAC").
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the explicit plain-text name title of the project board.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Maps parameters defining the classification rules of the tracked issue.
/// </summary>
public class JiraIssueTypeDto
{
    /// <summary>
    /// Gets or sets the name label of the task classification archetype (e.g., "Bug").
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Maps parameter properties detailing the resolution lifecycle step of the target task.
/// </summary>
public class JiraStatusDto
{
    /// <summary>
    /// Gets or sets the name text label of the active pipeline step (e.g., "Done").
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Acts as a JSON array serialization wrapper holding an inner collection of historical conversation records.
/// </summary>
public class JiraCommentCollectionDto
{
    /// <summary>
    /// Gets or sets the list layout containing individual user discussion comment rows.
    /// </summary>
    [JsonPropertyName("comments")]
    public List<JiraCommentDto> Comments { get; set; } = new();
}

/// <summary>
/// Maps an individual conversation record block or thread response row parsed out of historical task data.
/// </summary>
public class JiraCommentDto
{
    /// <summary>
    /// Gets or sets the unique identification key tracking the specific comment message string.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the raw body message payload text container.
    /// </summary>
    /// <remarks>
    /// Note: Sub-comments utilize the same complex ADF structural schema layouts in v3 API endpoints, 
    /// and are intercepted via generic object targets to preserve parsing integrity.
    /// </remarks>
    [JsonPropertyName("body")]
    public object? Body { get; set; }

    /// <summary>
    /// Gets or sets the ISO raw string text indicating when this discussion thread item was posted.
    /// </summary>
    [JsonPropertyName("created")]
    public string Created { get; set; } = string.Empty;
}