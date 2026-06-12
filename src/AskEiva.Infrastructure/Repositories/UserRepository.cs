using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AskEiva.Domain.Entities;

namespace AskEiva.Infrastructure.Repositories;

/// <summary>
/// Manages user authentication records and persistence operations, querying and inserting credentials 
/// directly inside Weaviate schema classes via REST and GraphQL.
/// </summary>
public class UserRepository
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserRepository"/> class with a pre-configured HTTP client factory.
    /// </summary>
    /// <param name="httpClient">An unmanaged or factory-allocated HTTP client configured for Weaviate database routing.</param>
    public UserRepository(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Searches the vector database using exact-match GraphQL filtering to locate a single user profile by their email address.
    /// </summary>
    /// <param name="email">The email identity address string under evaluation.</param>
    /// <returns>A populated domain <see cref="ApplicationUser"/> model if matched, otherwise null.</returns>
    public async Task<ApplicationUser?> FindByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;

        var targetEmail = email.Trim().ToLowerInvariant();

        // GraphQL query: Explicitly requests ONLY the user matching this exact email
        var gqlQuery = new
        {
            query = $$"""
            {
              Get {
                ApplicationUser(
                  where: {
                    path: ["email"],
                    operator: Equal,
                    valueText: "{{targetEmail}}"
                  }
                ) {
                  _additional {
                    id
                  }
                  email
                  passwordHash
                }
              }
            }
            """
        };

        // Post the GraphQL query to the standard Weaviate endpoint
        var response = await _httpClient.PostAsJsonAsync("/v1/graphql", gqlQuery);
        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<GqlResponse>();
        
        // Dig down into the GraphQL data payload tree safely
        var record = result?.Data?.Get?.ApplicationUser?.FirstOrDefault();
        if (record == null)
        {
            return null; 
        }

        return new ApplicationUser
        {
            Id = record.Additional?.Id ?? Guid.NewGuid().ToString(),
            Email = record.Email,
            PasswordHash = record.PasswordHash
        };
    }

    /// <summary>
    /// Inserts a new user record along with its associated cryptographic password hash directly into Weaviate storage.
    /// </summary>
    /// <param name="user">The domain user entity instance to persist.</param>
    /// <returns>An asynchronous task tracking completion of the storage write operation.</returns>
    public async Task CreateAsync(ApplicationUser user)
    {
        var payload = new
        {
            @class = "ApplicationUser",
            id = user.Id,
            properties = new Dictionary<string, object>
            {
                { "email", user.Email },
                { "passwordHash", user.PasswordHash }
            }
        };

        var response = await _httpClient.PostAsJsonAsync("/v1/objects", payload);
        response.EnsureSuccessStatusCode();
    }
}

// --- PRODUCTION GRAPHQL DESERIALIZATION SCHEMAS ---

/// <summary>
/// Root data wrapper reflecting the outer schema structure returned by Weaviate GraphQL requests.
/// </summary>
public class GqlResponse
{
    /// <summary>
    /// Gets or sets the primary payload envelope data object.
    /// </summary>
    [JsonPropertyName("data")]
    public GqlData? Data { get; set; }
}

/// <summary>
/// Maps the root 'Get' selection query payload returned inside a GraphQL search transaction.
/// </summary>
public class GqlData
{
    /// <summary>
    /// Gets or sets the inner query wrapper mapping indexed vector objects.
    /// </summary>
    [JsonPropertyName("Get")]
    public GqlGet? Get { get; set; }
}

/// <summary>
/// Isolates the targeted collection class partition tree parsed from Weaviate graph schemas.
/// </summary>
public class GqlGet
{
    /// <summary>
    /// Gets or sets the collection sequence matching the registered user fields.
    /// </summary>
    [JsonPropertyName("ApplicationUser")]
    public List<GqlUserRecord>? ApplicationUser { get; set; }
}

/// <summary>
/// Intermediate data transfer model mapping raw system properties from a retrieved database user entry.
/// </summary>
public class GqlUserRecord
{
    /// <summary>
    /// Gets or sets the literal email string value stored in the remote node properties.
    /// </summary>
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the secure cryptographic hash payload tracking string.
    /// </summary>
    [JsonPropertyName("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the system metadata envelope tracking deterministic node IDs.
    /// </summary>
    [JsonPropertyName("_additional")]
    public GqlAdditional? Additional { get; set; }
}

/// <summary>
/// Maps specialized Weaviate system properties such as structural record identifiers.
/// </summary>
public class GqlAdditional
{
    /// <summary>
    /// Gets or sets the system-generated tracking string key mapping this object space.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}