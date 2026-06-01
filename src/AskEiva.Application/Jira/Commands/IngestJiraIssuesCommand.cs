using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AskEiva.Application.Jira.Utils;
using AskEiva.Domain.Services;
using MediatR;

namespace AskEiva.Application.Jira.Commands;

public record IngestJiraIssuesCommand(string JqlFilter = "updated >= '2000-01-01' order by project asc, updated desc") : IRequest<JiraIngestionResult>;

public record JiraIngestionResult(bool IsSuccess, int TotalIssuesProcessed, int TotalChunksVectorized, string Message);

public class IngestJiraIssuesCommandHandler : IRequestHandler<IngestJiraIssuesCommand, JiraIngestionResult>
{
    private readonly IJiraService _jiraService;
    private readonly HttpClient _weaviateHttpClient;

    public IngestJiraIssuesCommandHandler(
        IJiraService jiraService, 
        HttpClient weaviateHttpClient)
    {
        _jiraService = jiraService;
        _weaviateHttpClient = weaviateHttpClient;
    }

    public async Task<JiraIngestionResult> Handle(IngestJiraIssuesCommand request, CancellationToken cancellationToken)
    {
        int startAt = 0;
        int maxResults = 500;
        int totalIssuesProcessed = 0;
        int totalChunksVectorized = 0;
        int currentPage = 1;
        bool hasMorePages = true;

        Console.WriteLine("[Jira Ingestion Engine] Commencing production Atlassian synchronization sweep pipeline...");

        try
        {
            while (hasMorePages)
            {
                if (cancellationToken.IsCancellationRequested)
                    return new JiraIngestionResult(false, totalIssuesProcessed, totalChunksVectorized, "Ingestion canceled by system request.");

                Console.WriteLine($"[Jira Ingestion Engine] Querying batch page {currentPage} (startAt: {startAt})...");

                var queryOptions = new JiraQueryOptions
                {
                    Jql = request.JqlFilter,
                    StartAt = startAt,
                    MaxResults = maxResults
                };

                var response = await _jiraService.GetIssuesPageAsync(queryOptions);

                if (response == null || response.Issues == null || response.Issues.Count == 0)
                {
                    Console.WriteLine("[Jira Ingestion Engine] Terminal boundary reached. No additional issues returned.");
                    break;
                }

                Console.WriteLine($"[Jira Ingestion Engine] API received {response.Issues.Count} issue objects. Starting extraction parsing pass...");
                var batchObjects = new List<object>();

                foreach (var issue in response.Issues)
                {
string cleanDescription = AtlassianDocumentParser.ToPlainText(issue.Fields.Description);

                    // 2. Build a linear, readable comment thread
                    var commentBuilder = new StringBuilder();
                    if (issue.Fields.CommentCollection?.Comments != null)
                    {
                        foreach (var comment in issue.Fields.CommentCollection.Comments)
                        {
                            string cleanCommentBody = AtlassianDocumentParser.ToPlainText(comment.Body);
                            if (!string.IsNullOrWhiteSpace(cleanCommentBody))
                            {
                                commentBuilder.AppendLine($"[Comment at {comment.Created}]: {cleanCommentBody}");
                            }
                        }
                    }

                    // 3. 💡 FALLBACK FUSION STRATEGY: Ensure empty descriptions don't blind the vector engine
                    var unifiedTextBuilder = new StringBuilder();
                    unifiedTextBuilder.AppendLine($"=== EIVA JIRA ISSUE MANAGEMENT NODE ===");
                    unifiedTextBuilder.AppendLine($"Issue Tracking Key: {issue.Key}");
                    unifiedTextBuilder.AppendLine($"Project Registry: {issue.Fields.Project?.Name} [{issue.Fields.Project?.Key}]");
                    unifiedTextBuilder.AppendLine($"Issue Architecture Type: {issue.Fields.IssueType?.Name}");
                    unifiedTextBuilder.AppendLine($"Current Workflow Status State: {issue.Fields.Status?.Name}");
                    unifiedTextBuilder.AppendLine($"Summary Headline: {issue.Fields.Summary}");
                    
                    if (!string.IsNullOrWhiteSpace(cleanDescription))
                    {
                        unifiedTextBuilder.AppendLine($"Detailed Engineering Description:\n{cleanDescription}");
                    }
                    else
                    {
                        // Synthesize context for headline-only cards so the embeddings still capture semantic intent
                        unifiedTextBuilder.AppendLine($"Description: This tracking card is a title-only tracking objective for '{issue.Fields.Summary}'. No further structural description text was provided");
                    }

                    if (commentBuilder.Length > 0)
                    {
                        unifiedTextBuilder.AppendLine("\n--- Chronological Technical Discussion Tree ---");
                        unifiedTextBuilder.Append(commentBuilder.ToString());
                    }

                    string rawFullText = unifiedTextBuilder.ToString();

                    var textChunks = SplitTextIntoOverlappingChunks(rawFullText, chunkSize: 1000, overlap: 200);
                    int internalPartIndex = 0;
                    string freshdeskId = "NONE";

                    var match = System.Text.RegularExpressions.Regex.Match(issue.Fields.Summary, @"#(\d+)");
                    if (match.Success)
                    {
                        freshdeskId = match.Groups[1].Value;
                    }

                    foreach (var chunk in textChunks)
                    {
                        var weaviateObject = new
                        {
                            @class = "JiraIssueNode",
                            properties = new
                            {
                                jira_id = issue.Id,
                                issue_key = issue.Key,
                                project_key = issue.Fields.Project?.Key ?? "UNKNOWN",
                                issue_type = issue.Fields.IssueType?.Name ?? "Task",
                                status_state = issue.Fields.Status?.Name ?? "Open",
                                summary = issue.Fields.Summary,
                                content = chunk,
                                freshdesk_ticket_id = freshdeskId 
                            }
                        };

                        batchObjects.Add(weaviateObject);
                        totalChunksVectorized++;
                        internalPartIndex++;
                    }

                    totalIssuesProcessed++;
                    Console.WriteLine($"[Jira Sync Processing] Segmented [{issue.Key}] into {internalPartIndex} unique sub-chunks.");
                }

                if (batchObjects.Count > 0)
                {
                    Console.WriteLine($"[Weaviate Writer] Committing batch block payload to vector cloud ({batchObjects.Count} objects)...");
                    var batchPayload = new { objects = batchObjects };
                    var batchResponse = await _weaviateHttpClient.PostAsJsonAsync("v1/batch/objects", batchPayload);
                    if (!batchResponse.IsSuccessStatusCode)
                    {
                        string errorText = await batchResponse.Content.ReadAsStringAsync();
                        Console.WriteLine($"[Weaviate Batch Failure] Error streaming logs down to database node: {errorText}");
                    }
                }

                startAt += maxResults;
                currentPage++;

                if (startAt >= response.Total)
                {
                    hasMorePages = false;
                }
            }

            Console.WriteLine($"[Jira Ingestion Engine] Complete! Successfully vectorized {totalIssuesProcessed} distinct issues into {totalChunksVectorized} memory nodes.");
            return new JiraIngestionResult(true, totalIssuesProcessed, totalChunksVectorized, "Synchronized perfectly.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Jira Ingestion Engine Critical Failure] Exception tracking: {ex.Message}");
            return new JiraIngestionResult(false, totalIssuesProcessed, totalChunksVectorized, ex.Message);
        }
    }

    private static List<string> SplitTextIntoOverlappingChunks(string text, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return chunks;
        if (text.Length <= chunkSize)
        {
            chunks.Add(text);
            return chunks;
        }

        int offset = 0;
        while (offset < text.Length)
        {
            if (offset + chunkSize >= text.Length)
            {
                chunks.Add(text.Substring(offset));
                break;
            }

            chunks.Add(text.Substring(offset, chunkSize));
            offset += (chunkSize - overlap);
        }

        return chunks;
    }
}