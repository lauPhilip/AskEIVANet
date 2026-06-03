using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AskEiva.Domain.Services;
using Microsoft.Extensions.Options;

namespace AskEiva.Infrastructure.Services;

public class JiraService : IJiraService
{
    private readonly HttpClient _httpClient;
    private readonly JiraConfiguration _config;

    public JiraService(HttpClient httpClient, IOptions<JiraConfiguration> configOptions)
    {
        _httpClient = httpClient;
        _config = configOptions.Value;

        if (_httpClient.BaseAddress == null && !string.IsNullOrEmpty(_config.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_config.BaseUrl);
        }
    }

public async Task<JiraSearchResponse?> GetIssuesPageAsync(JiraQueryOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Jql)) return null;

        int safeMax = options.MaxResults > 100 ? 100 : options.MaxResults;
        var escapedJql = Uri.EscapeDataString(options.Jql);
        
        // Construct our precise query string
        var cleanRelativeUrl = $"search/jql?jql={escapedJql}&startAt={options.StartAt}&maxResults={safeMax}&fields=summary,project,status,issuetype,description,comment";

        // 💡 DIAGNOSTIC LINE: Print out the absolute final string value to the terminal console
        Console.WriteLine($"[Outbound HTTP Inspect] Request String: {_httpClient.BaseAddress}{cleanRelativeUrl}");

        try
        {
            var response = await _httpClient.GetAsync(cleanRelativeUrl);
            if (!response.IsSuccessStatusCode) return null;

            string rawJson = await response.Content.ReadAsStringAsync();
            
            Console.WriteLine("=================================== JIRA RAW JSON PAYLOAD START ===================================");
            Console.WriteLine(rawJson.Length > 1000 ? rawJson.Substring(0, 1000) + "... [TRUNCATED]" : rawJson);
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