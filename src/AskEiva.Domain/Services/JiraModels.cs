using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AskEiva.Domain.Services;

public class JiraSearchResponse
{
    [JsonPropertyName("issues")]
    public List<JiraIssueRawDto> Issues { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

public class JiraIssueRawDto
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public JiraFieldsRawDto Fields { get; set; } = new();
}

public class JiraFieldsRawDto
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    // 💡 FIXED: Changed from string? to object?. 
    // This stops Jira's complex Atlassian Document Format (ADF) JSON graph from breaking the deserializer threshold!
    [JsonPropertyName("description")]
    public object? Description { get; set; }

    [JsonPropertyName("project")]
    public JiraProjectDto? Project { get; set; }

    [JsonPropertyName("issuetype")]
    public JiraIssueTypeDto? IssueType { get; set; }

    [JsonPropertyName("status")]
    public JiraStatusDto? Status { get; set; }

    [JsonPropertyName("comment")]
    public JiraCommentCollectionDto? CommentCollection { get; set; }
}

public class JiraProjectDto
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class JiraIssueTypeDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class JiraStatusDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class JiraCommentCollectionDto
{
    [JsonPropertyName("comments")]
    public List<JiraCommentDto> Comments { get; set; } = new();
}

public class JiraCommentDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    // 💡 FIXED: Comments also leverage the ADF complex structural format in v3
    [JsonPropertyName("body")]
    public object? Body { get; set; }

    [JsonPropertyName("created")]
    public string Created { get; set; } = string.Empty;
}