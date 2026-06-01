using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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

        if (string.IsNullOrEmpty(_config.BaseUrl))
            throw new ArgumentNullException(nameof(_config.BaseUrl), "Jira BaseUrl configuration property is missing inside local user-secrets.");

        // Initialize HttpClient boundaries tailored exactly to Atlassian's Gateway requirements
        _httpClient.BaseAddress = new Uri(_config.BaseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Compile and append the secure Basic Auth Header (Email:Token -> Base64 string payload)
        var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_config.Email}:{_config.ApiToken}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
    }

public async Task<JiraSearchResponse?> GetIssuesPageAsync(JiraQueryOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Jql)) return null;

        int safeMax = options.MaxResults > 100 ? 100 : options.MaxResults;
        var escapedJql = Uri.EscapeDataString(options.Jql);
        
        // 💡 FIXED: Rebuilt as a robust, absolute URI query layout string.
        // This ensures the Atlassian cloud proxy wrapper cannot strip the startAt offset tracking metric!
        var relativeUrl = $"rest/api/3/search/jql?jql={escapedJql}&startAt={options.StartAt}&maxResults={safeMax}&fields=summary,project,status,issuetype,description,comment";
        
        // Combine the configured BaseUrl cleanly with the relative path strings
        var baseAddressString = _httpClient.BaseAddress?.ToString() ?? _config.BaseUrl;
        if (!baseAddressString.EndsWith("/")) baseAddressString += "/";
        
        var absoluteUrl = new Uri(new Uri(baseAddressString), relativeUrl).ToString();

        try
        {
            // 💡 Print the exact URL to the terminal so we can audit the startAt moving offset live
            Console.WriteLine($"[Jira Network Outbound] Routing Target URI: {absoluteUrl}");

            var response = await _httpClient.GetAsync(absoluteUrl);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(15);
                await Task.Delay(retryAfter);
                return await GetIssuesPageAsync(options);
            }

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Jira Service API Error] Failed querying at startAt {options.StartAt}. Status: {response.StatusCode}, Context: {errorBody}");
                return null;
            }

            return await response.Content.ReadFromJsonAsync<JiraSearchResponse>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Jira Service Critical Failure] Request stream fracture: {ex.Message}");
            return null;
        }
    }
}