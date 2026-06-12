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

/// <summary>
/// MediatR command to synchronize, process, and vectorize corporate Jira issues based on a JQL tracking filter target.
/// </summary>
/// <param name="JqlFilter">The Atlassian Jira Query Language string used to target specific issue updates.</param>
public record IngestJiraIssuesCommand(string JqlFilter = "updated >= '2000-01-01' order by key asc") : IRequest<JiraIngestionResult>;

/// <summary>
/// Represents the operation results and execution counts of the Jira ticket ingestion run.
/// </summary>
/// <param name="IsSuccess">Indicates if the synchronization completed successfully without throwing exceptions.</param>
/// <param name="TotalIssuesProcessed">The total number of individual Jira tasks parsed during the execution.</param>
/// <param name="TotalChunksVectorized">The total number of text segments generated and stored into the vector database.</param>
/// <param name="Message">Descriptive text summarizing the final outcome or failure details.</param>
public record JiraIngestionResult(bool IsSuccess, int TotalIssuesProcessed, int TotalChunksVectorized, string Message);

/// <summary>
/// Handles the fetching of external issues from Atlassian APIs, unifies metadata with comment histories, 
/// segments long text fields, and saves vector payloads into the target cloud cluster database.
/// </summary>
public class IngestJiraIssuesCommandHandler : IRequestHandler<IngestJiraIssuesCommand, JiraIngestionResult>
{
    private readonly IJiraService _jiraService;
    private readonly HttpClient _weaviateHttpClient;

    /// <summary>
    /// Initializes a new instance of the handler using the external Jira api client and a vector storage engine http channel client.
    /// </summary>
    /// <param name="jiraService">The external service client proxy handling Atlassian API calls.</param>
    /// <param name="weaviateHttpClient">An HTTP client instance configured to talk to the vector engine endpoint.</param>
    public IngestJiraIssuesCommandHandler(
        IJiraService jiraService, 
        HttpClient weaviateHttpClient)
    {
        _jiraService = jiraService;
        _weaviateHttpClient = weaviateHttpClient;
    }

    /// <summary>
    /// Executes the Jira issue synchronization loop, fetching paginated task records, parsing formatting layouts, 
    /// tracking cross-system ticket mappings, and submitting batch vectors.
    /// </summary>
    /// <param name="request">The instruction parameters containing the target JQL criteria strings.</param>
    /// <param name="cancellationToken">Token used to safely intercept and cancel the ongoing background data ingestion tasks.</param>
    /// <returns>A results instance containing task counts and database insertion statistics.</returns>
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

                // Pull the next set of tracking issue payloads using the configuration parameters
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
                    // Clean rich-text formatting tags out of the main description body
                    string cleanDescription = AtlassianDocumentParser.ToPlainText(issue.Fields.Description);

                    // 1. Unify and reconstruct sequential chat replies and comment fields into an audit log
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

                    // 2. Metadata Compilation: Synthesize missing fields to ensure quality vector embeddings
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
                        // Fallback message ensures headline-only tickets still possess adequate contextual embeddings weights
                        unifiedTextBuilder.AppendLine($"Description: This tracking card is a title-only tracking objective for '{issue.Fields.Summary}'. No further structural description text was provided");
                    }

                    if (commentBuilder.Length > 0)
                    {
                        unifiedTextBuilder.AppendLine("\n--- Chronological Technical Discussion Tree ---");
                        unifiedTextBuilder.Append(commentBuilder.ToString());
                    }

                    string rawFullText = unifiedTextBuilder.ToString();

                    // Segment the combined text block into sliding chunks to preserve continuity in storage nodes
                    var textChunks = SplitTextIntoOverlappingChunks(rawFullText, chunkSize: 1000, overlap: 200);
                    int internalPartIndex = 0;
                    string freshdeskId = "NONE";

                    // Discover linked tracking ticket values using key hashtag extraction loops
                    var match = System.Text.RegularExpressions.Regex.Match(issue.Fields.Summary, @"#(\d+)");
                    if (match.Success)
                    {
                        freshdeskId = match.Groups[1].Value;
                    }

                    foreach (var chunk in textChunks)
                    {
                        // Model tracking properties exactly mapping the expected database layout schemas
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
                    // Dispatch the parsed payloads as a single unified batch network request to optimize bandwidth
                    Console.WriteLine($"[Weaviate Writer] Committing batch block payload to vector cloud ({batchObjects.Count} objects)...");
                    var batchPayload = new { objects = batchObjects };
                    var batchResponse = await _weaviateHttpClient.PostAsJsonAsync("v1/batch/objects", batchPayload);
                    if (!batchResponse.IsSuccessStatusCode)
                    {
                        string errorText = await batchResponse.Content.ReadAsStringAsync();
                        Console.WriteLine($"[Weaviate Batch Failure] Error streaming logs down to database node: {errorText}");
                    }
                }

                // Increment offsets to traverse downstream paginated index spaces
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

    /// <summary>
    /// Processes high-length string records into a list of structured sub-segments using a fixed sliding window index range.
    /// </summary>
    /// <param name="text">The raw text body content to slice.</param>
    /// <param name="chunkSize">The relative maximum layout size allocated to any single split fragment.</param>
    /// <param name="overlap">The character length shared between adjacent index segments to preserve text flow context boundaries.</param>
    /// <returns>A collection of individual text segment items.</returns>
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