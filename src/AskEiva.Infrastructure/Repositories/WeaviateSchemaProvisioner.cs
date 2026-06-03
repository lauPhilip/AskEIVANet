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

    private async Task EnsureKnowledgeNodeClassAsync()
    {
        var checkUrl = "v1/schema/KnowledgeNode";
        var createUrl = "v1/schema";

        try
        {
            var response = await _httpClient.GetAsync(checkUrl);
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("[Weaviate Provisioner] Class 'KnowledgeNode' verified.");
                return;
            }

            // Define the structural layout schema blueprint matching your live console dashboard setup configurations
            var knowledgeNodeSchema = new
            {
                @class = "KnowledgeNode",
                description = "Unified multi-source semantic vector store cluster mapping historical context inputs for AskEiva.",
                vectorizer = "text2vec-mistral", // Maps to mistral-embed vector engine
                moduleConfig = new
                {
                    @text2vec_mistral = new { }
                },
                vectorIndexConfig = new
                {
                    distance = "cosine",
                    vectorCacheMaxObjects = 500000,
                    quantizer = new { enabled = true, type = "pq", segments = 0, encoder = new { type = "kmeans" } } // RQ 8-bit variant matching cluster blueprint
                },
                properties = new object[]
                {
                    new { name = "source_id", dataType = new[] { "text" }, tokenization = "word" },
                    new { name = "data_type", dataType = new[] { "text" }, tokenization = "word" },
                    new { name = "subject", dataType = new[] { "text" }, tokenization = "word" },
                    new { name = "content", dataType = new[] { "text" }, tokenization = "word" },
                    new { name = "is_distilled", dataType = new[] { "boolean" } },
                    new { name = "url", dataType = new[] { "text" }, tokenization = "field" },
                    new { name = "status", dataType = new[] { "int" } },
                    new { name = "priority", dataType = new[] { "int" } },
                    new { name = "tags", dataType = new[] { "text[]" } }
                }
            };

            Console.WriteLine("[Weaviate Provisioner] Initializing schema structure template instantiation for collection: KnowledgeNode...");
            var createResponse = await _httpClient.PostAsJsonAsync(createUrl, knowledgeNodeSchema);
            createResponse.EnsureSuccessStatusCode();
            Console.WriteLine("[Weaviate Provisioner] Class 'KnowledgeNode' successfully provisioned on remote cluster endpoints.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Weaviate Provisioner Critical Exception]: {ex.Message}");
        }
    }
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

                await EnsureKnowledgeNodeClassAsync();
                await EnsureJiraSchemaAsync();
                await EnsureGraphContextChainClassAsync();
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "An unhandled exception collapsed the global schema configuration loop.");
            }
        }
private async Task EnsureGraphContextChainClassAsync()
    {
        var checkUrl = "v1/schema/GraphContextChain";
        var createUrl = "v1/schema";

        try
        {
            var response = await _httpClient.GetAsync(checkUrl);
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("[Weaviate Provisioner] Class 'GraphContextChain' verified.");
                return;
            }

            // 💡 NEW: Define the structural schema blueprint for the text-graph scenario paths
            var chainSchema = new
            {
                @class = "GraphContextChain",
                description = "Stores multi-hop context paths linking customer tickets directly to technical scenarios and software updates text spans.",
                vectorizer = "text2vec-mistral", // Automatically vectors your text chains
                moduleConfig = new
                {
                    @text2vec_mistral = new
                    {
                        model = "mistral-embed",
                        type = "text"
                    }
                },
                vectorIndexConfig = new
                {
                    distance = "cosine"
                },
                properties = new object[]
                {
                    new { name = "ticket_id", dataType = new[] { "text" }, tokenization = "word", description = "The domain ticket ID tracker asset key (e.g. FD-82022)." },
                    new { name = "ticket_title", dataType = new[] { "text" }, tokenization = "word", description = "The explicit or derived anchor title of the support query context." },
                    new { name = "main_product_context", dataType = new[] { "text" }, tokenization = "field", description = "The primary EIVA software product affected (e.g. NaviPac)." },
                    new { name = "scenario_type", dataType = new[] { "text" }, tokenization = "field", description = "The mapped routing classification type category." },
                    new { name = "shared_path_chain", dataType = new[] { "text" }, tokenization = "word", description = "The raw complete text-based continuous multi-hop string sequence path." },
                    new { name = "predicates", dataType = new[] { "text[]" }, description = "List of relationship tokens tracking the connection sequence steps." },
                    new { name = "environmental_context_summary", dataType = new[] { "text" }, tokenization = "word", description = "Enriched text summary mapping intersecting ecosystem settings, versions, or conditions." },
                    new { name = "confidence_score", dataType = new[] { "int" }, description = "Calculated link probability confidence scalar metric multiplied by 100." }
                }
            };

            Console.WriteLine("[Weaviate Provisioner] Provisioning new multi-hop text chain collection schema class: GraphContextChain...");
            var createResponse = await _httpClient.PostAsJsonAsync(createUrl, chainSchema);
            createResponse.EnsureSuccessStatusCode();
            Console.WriteLine("[Weaviate Provisioner] Class 'GraphContextChain' successfully provisioned on remote cluster endpoints.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Weaviate Provisioner Critical Exception - GraphContextChain]: {ex.Message}");
        }
    }

        private async Task EnsureJiraSchemaAsync()
    {
        try
        {
            // Verify if the Jira collection already exists in your Weaviate cloud instance
            var checkResponse = await _httpClient.GetAsync("v1/schema/JiraIssueNode");
            if (checkResponse.IsSuccessStatusCode)
            {
                Console.WriteLine("[Weaviate Provisioner] Class 'JiraIssueNode' already provisioned.");
                return;
            }

            // Define the explicit schema matching your production Jira domain attributes
            var jiraSchema = new
            {
                @class = "JiraIssueNode",
                description = "Stores segmented, semantic text chunks extracted from EIVA's Atlassian Jira issue logs.",
                vectorizer = "text2vec-mistral", // Ties directly into your existing vectorizer setup
                properties = new[]
                {
                    new { name = "jira_id", dataType = new[] { "string" }, description = "The raw database GUID identifier string from Jira." },
                    new { name = "issue_key", dataType = new[] { "string" }, description = "The readable issue tag key (e.g., NAVIPAC-1234)." },
                    new { name = "project_key", dataType = new[] { "string" }, description = "The core software product abbreviation code." },
                    new { name = "issue_type", dataType = new[] { "string" }, description = "The task classification archetype (Bug, Feature, Task)." },
                    new { name = "status_state", dataType = new[] { "string" }, description = "The current lifecycle workflow state." },
                    new { name = "summary", dataType = new[] { "text" }, description = "The plain text subject line headline of the issue card." },
                    new { name = "content", dataType = new[] { "text" }, description = "The flattened clean description text combined with chronological comment lines." }
                }
            };

            var createResponse = await _httpClient.PostAsJsonAsync("v1/schema", jiraSchema);
            if (createResponse.IsSuccessStatusCode)
            {
                Console.WriteLine("[Weaviate Provisioner] Success! Class 'JiraIssueNode' has been provisioned globally.");
            }
            else
            {
                string errors = await createResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"[Weaviate Provisioner] Critical error building Jira schema template: {errors}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Weaviate Provisioner] Jira schema generation channel encountered an anomaly: {ex.Message}");
        }
    }


        private object CreateSoftwareReleaseSchema()
                {
                    return new
                    {
                        @class = "SoftwareReleaseNode",
                        description = "Textual chunks and metadata harvested from product software releases and patches",
                        vectorizer = "text2vec-mistral", 
                        moduleConfig = new
                        {
                            @text2vec_mistral = new
                            {
                                model = "mistral-embed", 
                                type = "text"
                            }
                        }, 
                        properties = new object[]
                        {
                            new { name = "group_category", dataType = new[] { "text" }, description = "The overall engineering suite category classification (e.g. NaviSuite).", tokenization = "field" },
                            new { name = "product", dataType = new[] { "text" }, description = "The targeted product category name.", tokenization = "field" },
                            new { name = "version", dataType = new[] { "text" }, description = "The concrete version signature string.", tokenization = "field" },
                            new { name = "full_version_title", dataType = new[] { "text" }, description = "The human-readable combined product version string (e.g. NaviPac – 4.13).", tokenization = "field" }, 
                            new { name = "release_date", dataType = new[] { "date" }, description = "The official timestamp deployment parameter." },
                            new { name = "metadata_note", dataType = new[] { "text" }, description = "Special distribution footnotes, system constraints, or cross-compatibility warnings.", tokenization = "word" },
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