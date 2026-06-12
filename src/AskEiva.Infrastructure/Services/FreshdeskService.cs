using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AskEiva.Domain.Services;

namespace AskEiva.Infrastructure.Services;

/// <summary>
/// Implements the <see cref="IFreshdeskService"/> contract using an authorized, resilient 
/// <see cref="HttpClient"/> pipeline to pull historical customer technical support interactions 
/// and threaded sub-conversations from the Freshdesk REST API.
/// </summary>
public class FreshdeskService : IFreshdeskService
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="FreshdeskService"/> class with a pre-configured HTTP client.
    /// </summary>
    /// <param name="httpClient">An unmanaged or factory-allocated HTTP client containing authorized Freshdesk base configurations.</param>
    public FreshdeskService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Fetches a targeted pagination window page of support tickets, using defensive rate-limit back-off interceptors 
    /// and a deep historical anchor filter to pull across all ticket lifecycles.
    /// </summary>
    /// <param name="page">The current sequential page index integer to request from the API endpoint.</param>
    /// <param name="perPage">The quantity size limit count of records to absorb per page window (defaults to 30).</param>
    /// <returns>A collection stream sequence of raw data transfer ticket records.</returns>
    /// <remarks>
    /// Note: To bypass state-filtering limitations, this pipeline strips status restriction parameters completely, 
    /// establishing a historical anchor pointing to '2010-01-01' to fetch the complete enterprise ledger.
    /// </remarks>
    public async Task<IEnumerable<FreshdeskTicketDto>> GetTicketsPageAsync(int page, int perPage = 30)
    {
        // Strip out custom status arrays and filter properties entirely to fetch the full historical ledger.
        var historicalAnchor = Uri.EscapeDataString("2010-01-01T00:00:00Z");
        var url = $"tickets?page={page}&per_page={perPage}&updated_since={historicalAnchor}&include=description";

        try
        {
            var response = await _httpClient.GetAsync(url);
            
            // Intercept HTTP 421 / 429 rate limitation blocks cleanly
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(10);
                Console.WriteLine($"[Freshdesk Guard] Rate limit hit. Backing off for {retryAfter.TotalSeconds}s...");
                
                await Task.Delay(retryAfter);
                return await GetTicketsPageAsync(page, perPage); // Recursively retry after back-off delay finishes
            }

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Freshdesk Service] API failure on page {page}. Status: {response.StatusCode}, Details: {errorBody}");
                return Enumerable.Empty<FreshdeskTicketDto>();
            }

            var tickets = await response.Content.ReadFromJsonAsync<List<FreshdeskTicketDto>>();
            return tickets ?? Enumerable.Empty<FreshdeskTicketDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Freshdesk Service] Exception on page {page}: {ex.Message}");
            return Enumerable.Empty<FreshdeskTicketDto>();
        }
    }

    /// <summary>
    /// Queries the detailed dialogue timeline sequence for an individual support ticket to extract historical replies and agent answers.
    /// </summary>
    /// <param name="ticketId">The system-assigned database unique key of the anchor ticket.</param>
    /// <returns>A collection sequence containing individual threaded discussion replies and notes.</returns>
    public async Task<IEnumerable<FreshdeskConversationDto>> GetTicketConversationsAsync(long ticketId)
    {
        var url = $"tickets/{ticketId}/conversations";
        try
        {
            var response = await _httpClient.GetAsync(url);
            
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
                await Task.Delay(retryAfter);
                return await GetTicketConversationsAsync(ticketId); // Recursively retry after back-off delay finishes
            }

            if (!response.IsSuccessStatusCode) return Enumerable.Empty<FreshdeskConversationDto>();
            return await response.Content.ReadFromJsonAsync<List<FreshdeskConversationDto>>() ?? Enumerable.Empty<FreshdeskConversationDto>();
        }
        catch
        {
            return Enumerable.Empty<FreshdeskConversationDto>();
        }
    }

    /// <summary>
    /// Private utility envelope class helper used to map deserialization graphs from Freshdesk's structural search endpoint collections.
    /// </summary>
    private class FreshdeskSearchRoot
    {
        /// <summary>
        /// Gets or sets the list collection of data transfer ticket results.
        /// </summary>
        public List<FreshdeskTicketDto> Results { get; set; } = [];
    }
}