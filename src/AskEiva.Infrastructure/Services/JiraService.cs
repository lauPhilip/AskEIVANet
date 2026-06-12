using System;
using System.Net.Http;
using System.Threading.Tasks;
using AskEiva.Domain.Services;
using Microsoft.Extensions.Options;

namespace AskEiva.Infrastructure.Services;

/// <summary>
/// Implements the <see cref="IJiraService"/> contract using an authorized <see cref="HttpClient"/> 
/// pipeline to execute JQL (Jira Query Language) searches, pulling issue tracker details and bug tickets 
/// directly from Atlassian Jira cloud instances.
/// </summary>
public class JiraService : IJiraService
{
    private readonly HttpClient _httpClient;
    private readonly JiraConfiguration _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="JiraService"/> class, automatically configuring the 
    /// fallback BaseAddress properties using strongly-typed application configuration settings.
    /// </summary>
    /// <param name="httpClient">An unmanaged or factory-allocated HTTP client containing core network configurations.</param>
    /// <param name="configOptions">The snapshot configuration option provider container containing specific Jira instance secret metadata settings.</param>
    public JiraService(HttpClient httpClient, IOptions<JiraConfiguration> configOptions)
    {
        _httpClient = httpClient;
        _config = configOptions.Value;

        if (_httpClient.BaseAddress == null && !string.IsNullOrEmpty(_config.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_config.BaseUrl);
        }
    }

    /// <summary>
    /// Dispatches a paginated JQL search transaction query request, pulling specific fields such as summaries, 
    /// project tags, descriptions, and comments while logging trace snapshots to check data health.
    /// </summary>
    /// <param name="options">The structural search parameters tracking the JQL target instruction strings, starting indices, and result count caps.</param>
    /// <returns>A deserialized <see cref="JiraSearchResponse"/> graph object holding the requested issue data if successful; otherwise, null.</returns>
    public async Task<JiraSearchResponse?> GetIssuesPageAsync(JiraQueryOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Jql)) return null;

        // Force a safe results cap at a maximum threshold of 100 items to protect memory allocation bounds
        int safeMax = options.MaxResults > 100 ? 100 : options.MaxResults;
        var escapedJql = Uri.EscapeDataString(options.Jql);
        
        // Construct our precise query string targeting exact system data requirements
        var cleanRelativeUrl = $"search/jql?jql={escapedJql}&startAt={options.StartAt}&maxResults={safeMax}&fields=summary,project,status,issuetype,description,comment";

        // DIAGNOSTIC LINE: Print out the absolute final string value to the terminal console
        Console.WriteLine($"[Outbound HTTP Inspect] Request String: {_httpClient.BaseAddress}{cleanRelativeUrl}");

        try
        {
            var response = await _httpClient.GetAsync(cleanRelativeUrl);
            if (!response.IsSuccessStatusCode) return null;

            string rawJson = await response.Content.ReadAsStringAsync();
            
            Console.WriteLine("=================================== JIRA RAW JSON PAYLOAD START ===================================");
            Console.WriteLine(rawJson.Length > 1000 ? string.Concat(rawJson.AsSpan(0, 1000), "... [TRUNCATED]") : rawJson);
            Console.WriteLine("==================================== JIRA RAW JSON PAYLOAD END ====================================");

            var optionsSerializer = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return System.Text.Json.JsonSerializer.Deserialize<JiraSearchResponse>(rawJson, optionsSerializer);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Jira Service Critical Failure] GET stream fracture: {ex.Message}");
            return null;
        }
    }
}