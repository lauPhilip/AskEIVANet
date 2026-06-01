using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AskEiva.Domain.Services;

public class JiraSearchResponse
{
    [JsonPropertyName("startAt")]
    public int StartAt { get; set; }

    [JsonPropertyName("maxResults")]
    public int MaxResults { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("issues")]
    public List<JiraIssueRawDto> Issues { get; set; } = new();
}

public class JiraIssueRawDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("self")]
    public string Self { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public JiraFieldsRawDto Fields { get; set; } = new();
}

public class JiraFieldsRawDto
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("project")]
    public JiraProjectRawDto Project { get; set; } = new();

    [JsonPropertyName("status")]
    public JiraStatusRawDto Status { get; set; } = new();

    [JsonPropertyName("issuetype")]
    public JiraIssueTypeRawDto IssueType { get; set; } = new();

    [JsonPropertyName("description")]
    public object? Description { get; set; }

    [JsonPropertyName("comment")]
    public JiraCommentResponseRawDto? CommentCollection { get; set; }
}

public class JiraProjectRawDto
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class JiraStatusRawDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class JiraIssueTypeRawDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class JiraCommentResponseRawDto
{
    [JsonPropertyName("comments")]
    public List<JiraCommentRawDto> Comments { get; set; } = new();
}

public class JiraCommentRawDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public object? Body { get; set; }
 
    [JsonPropertyName("created")]
    public string Created { get; set; } = string.Empty;
    }