using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AskEiva.Infrastructure.Repositories
{
    public class WeaviateSchemaProvisioner
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WeaviateSchemaProvisioner> _logger;

        public WeaviateSchemaProvisioner(HttpClient httpClient, ILogger<WeaviateSchemaProvisioner> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        /// <summary>
        /// Orchestrates the strict collection schema provisioning checks for all core dependencies matching domain specifications.
        /// </summary>
        public async Task EnsureSchemaAsync()
        {
            try
            {
                _logger.LogInformation("Checking Weaviate for existing Identity and Release metrics schemas...");

                // 1. Provision the Identity Schema Layer
                var userSchema = new
                {
                    @class = "ApplicationUser",
                    description = "Stores encrypted core identity accounts for the AskEiva system mapping matrix.",
                    vectorizer = "none",
                    properties = new object[]
                    {
                        new { name = "email", dataType = new[] { "text" }, description = "The unique operational email identifier.", tokenization = "field" },
                        new { name = "passwordHash", dataType = new[] { "text" }, description = "The cryptographically secure hashed password string." }
                    }
                };
                await ProvisionClassIfNeededAsync("ApplicationUser", userSchema);

                // 2. Provision the Software Release Notes Collection Schema Layer
                var releaseNotesSchema = CreateSoftwareReleaseSchema();
                await ProvisionClassIfNeededAsync("SoftwareReleaseNode", releaseNotesSchema);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "An unhandled exception collapsed the global schema configuration loop.");
            }
        }

        private object CreateSoftwareReleaseSchema()
        {
            return new
            {
                @class = "SoftwareReleaseNode",
                description = "Textual chunks and metadata harvested from product software releases and patches",
                vectorizer = "text2vec-openai", // Targets specific project LLM clustering models
                properties = new object[]
                {
                    new { name = "product", dataType = new[] { "text" }, description = "The targeted product category name.", tokenization = "field" },
                    new { name = "version", dataType = new[] { "text" }, description = "The concrete version signature string.", tokenization = "field" },
                    new { name = "release_date", dataType = new[] { "date" }, description = "The official timestamp deployment parameter." },
                    new { name = "section_header", dataType = new[] { "text" }, description = "The designated sub-module code block origin header line." },
                    new { name = "content_chunk", dataType = new[] { "text" }, description = "The segmented release log bullet descriptions tracking alterations." },
                    new { name = "ref_tickets", dataType = new[] { "text" }, description = "Associated Freshdesk or Jira ticket tracking tokens.", tokenization = "word" }
                }
            };
        }

        private async Task ProvisionClassIfNeededAsync(string className, object schema)
        {
            try
            {
                var checkResponse = await _httpClient.GetAsync($"/v1/schema/{className}");
                
                if (checkResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Schema '{className}' already provisioned in Weaviate.");
                    return;
                }

                _logger.LogWarning($"Schema '{className}' not found. Initializing provisioning pipeline...");

                var jsonPayload = JsonSerializer.Serialize(schema);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var provisionResponse = await _httpClient.PostAsync("/v1/schema", content);

                if (provisionResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Successfully provisioned '{className}' collection structure into Weaviate.");
                }
                else
                {
                    var errorDetails = await provisionResponse.Content.ReadAsStringAsync();
                    _logger.LogError($"Critical Schema Provisioning Failure for {className}: {provisionResponse.StatusCode} - {errorDetails}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed checking or instantiating target class matrix template: {className}");
                throw; // Rethrow to let the parent task block handle context loop collapse gracefully
            }
        }
    }
}