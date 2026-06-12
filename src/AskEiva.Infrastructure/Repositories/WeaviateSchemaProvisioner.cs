using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AskEiva.Infrastructure.Repositories
{
/// <summary>
/// Provides idempotent database initialization pipelines responsible for checking, creating, 
/// and updating Weaviate vector collection schemas, indexing rules, and text compression topologies.
/// </summary>
public class WeaviateSchemaProvisioner
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WeaviateSchemaProvisioner> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="WeaviateSchemaProvisioner"/> class with an authorized client.
        /// </summary>
        /// <param name="httpClient">An unmanaged or factory-allocated HTTP client pre-configured with Weaviate connection headers.</param>
        /// <param name="logger">The application logging instance used to track system deployment output.</param>
        public WeaviateSchemaProvisioner(HttpClient httpClient, ILogger<WeaviateSchemaProvisioner> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        /// <summary>
        /// Coordinates the chronological execution of class provisioning steps to confirm all target data layers 
        /// are verified or configured inside the Weaviate instance.
        /// </summary>
        /// <returns>An asynchronous task tracking deployment pipeline operations.</returns>
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
                    vectorIndexType = "flat", 
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

                // Provisioning the DocumentationLibrary using the exact same factory pattern helper method
                var documentationLibrarySchema = CreateDocumentationLibrarySchema();
                await ProvisionClassIfNeededAsync("DocumentationLibrary", documentationLibrarySchema);

                // 3. Provision the remaining structural collection schema layers
                await EnsureKnowledgeNodeClassAsync();
                await EnsureJiraSchemaAsync();
                await EnsureGraphContextChainClassAsync();
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "An unhandled exception collapsed the global schema configuration loop.");
            }
        }
        /// <summary>
        /// Compiles the vector index rules and property arrays required to initialize the DocumentationLibrary schema.
        /// </summary>
        private object CreateDocumentationLibrarySchema()
        {
            return new
            {
                @class = "DocumentationLibrary",
                description = "Vector storage collection tracking scraped technical manuals, articles, and documentation books assets for AskEiva.",
                vectorizer = "text2vec-mistral",
                vectorIndexType = "hnsw",
                moduleConfig = new
                {
                    @text2vec_mistral = new
                    {
                        model = "mistral-embed"
                    }
                },
                vectorIndexConfig = new
                {
                    distance = "cosine"
                },
                properties = new object[]
                {
                    new { name = "document_id", dataType = new[] { "text" }, tokenization = "word", description = "The unique alphanumeric index trace mapping code." },
                    new { name = "title", dataType = new[] { "text" }, tokenization = "word", description = "The clean descriptive header name of the reference manual section." },
                    new { name = "document_type", dataType = new[] { "text" }, tokenization = "field", description = "The asset grouping category classification type token." },
                    new { name = "content", dataType = new[] { "text" }, tokenization = "word", description = "The processed text payload chunk body clear of layout scripts html elements." },
                    new { name = "url", dataType = new[] { "text" }, tokenization = "field", description = "Direct hypermedia address routing link." },
                    new { name = "image_urls", dataType = new[] { "text[]" }, description = "Array list collection storing raw locations of images paths." },
                    new { name = "tags", dataType = new[] { "text[]" }, description = "Dynamic metadata keywords indicating associated technical EIVA suite products and features." }
                }
            };
        }

        /// <summary>
        /// Verifies or constructs the foundational multi-source ticket chunk class schema using an optimized HNSW structure 
        /// combined with a Product Quantizer (PQ) to maximize operational vector caching thresholds.
        /// </summary>
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

                var knowledgeNodeSchema = new
                {
                    @class = "KnowledgeNode",
                    description = "Unified multi-source semantic vector store cluster mapping historical context inputs for AskEiva.",
                    vectorizer = "text2vec-mistral",
                    vectorIndexType="hnsw",
                    moduleConfig = new
                    {
                        @text2vec_mistral = new { }
                    },
                    vectorIndexConfig = new
                    {
                        distance = "cosine",
                        vectorCacheMaxObjects = 500000,
                        quantizer = new { enabled = true, type = "pq", segments = 0, encoder = new { type = "kmeans" } }
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

                var createResponse = await _httpClient.PostAsJsonAsync(createUrl, knowledgeNodeSchema);
                createResponse.EnsureSuccessStatusCode();
                Console.WriteLine("[Weaviate Provisioner] Class 'KnowledgeNode' successfully provisioned.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Weaviate Provisioner Critical Exception - KnowledgeNode]: {ex.Message}");
            }
        }

        /// <summary>
        /// Verifies or provisions the storage schema designed to preserve multi-hop knowledge graph relationships 
        /// extracted during contextual distillation loops.
        /// </summary>
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

                var chainSchema = new
                {
                    @class = "GraphContextChain",
                    description = "Stores multi-hop context paths linking customer tickets directly to technical scenarios and software updates text spans.",
                    vectorizer = "text2vec-mistral",
                    vectorIndexType="hnsw",
                    moduleConfig = new
                    {
                        @text2vec_mistral = new { model = "mistral-embed", type = "text" }
                    },
                    vectorIndexConfig = new
                    {
                        distance = "cosine"
                    },
                    properties = new object[]
                    {
                        new { name = "ticket_id", dataType = new[] { "text" }, tokenization = "word" },
                        new { name = "ticket_title", dataType = new[] { "text" }, tokenization = "word" },
                        new { name = "main_product_context", dataType = new[] { "text" }, tokenization = "field" },
                        new { name = "scenario_type", dataType = new[] { "text" }, tokenization = "field" },
                        new { name = "shared_path_chain", dataType = new[] { "text" }, tokenization = "word" },
                        new { name = "predicates", dataType = new[] { "text[]" } },
                        new { name = "environmental_context_summary", dataType = new[] { "text" }, tokenization = "word" },
                        new { name = "confidence_score", dataType = new[] { "int" } }
                    }
                };

                var createResponse = await _httpClient.PostAsJsonAsync(createUrl, chainSchema);
                createResponse.EnsureSuccessStatusCode();
                Console.WriteLine("[Weaviate Provisioner] Class 'GraphContextChain' successfully provisioned.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Weaviate Provisioner Critical Exception - GraphContextChain]: {ex.Message}");
            }
        }

        /// <summary>
        /// Verifies or provisions the vector storage schema tracking Atlassian Jira issue trackers and software bugs.
        /// </summary>
        private async Task EnsureJiraSchemaAsync()
        {
            try
            {
                var checkResponse = await _httpClient.GetAsync("v1/schema/JiraIssueNode");
                if (checkResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine("[Weaviate Provisioner] Class 'JiraIssueNode' verified.");
                    return;
                }

                var jiraSchema = new
                {
                    @class = "JiraIssueNode",
                    description = "Stores segmented, semantic text chunks extracted from EIVA's Atlassian Jira issue logs.",
                    vectorizer = "text2vec-mistral",
                    vectorIndexType="hnsw",
                    properties = new[]
                    {
                        new { name = "jira_id", dataType = new[] { "string" } },
                        new { name = "issue_key", dataType = new[] { "string" } },
                        new { name = "project_key", dataType = new[] { "string" } },
                        new { name = "issue_type", dataType = new[] { "string" } },
                        new { name = "status_state", dataType = new[] { "string" } },
                        new { name = "summary", dataType = new[] { "text" } },
                        new { name = "content", dataType = new[] { "text" } }
                    }
                };

                var createResponse = await _httpClient.PostAsJsonAsync("v1/schema", jiraSchema);
                createResponse.EnsureSuccessStatusCode();
                Console.WriteLine("[Weaviate Provisioner] Class 'JiraIssueNode' successfully provisioned.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Weaviate Provisioner] Jira schema generation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Compiles the vector layout definition mapping out processed changelogs and firmware deployment metrics.
        /// </summary>
        private object CreateSoftwareReleaseSchema()
        {
            return new
            {
                @class = "SoftwareReleaseNode",
                description = "Textual chunks and metadata harvested from product software releases and patches",
                vectorizer = "text2vec-mistral",
                vectorIndexType="hnsw",
                moduleConfig = new
                {
                    @text2vec_mistral = new { model = "mistral-embed", type = "text" }
                },
                properties = new object[]
                {
                    new { name = "group_category", dataType = new[] { "text" }, tokenization = "field" },
                    new { name = "product", dataType = new[] { "text" }, tokenization = "field" },
                    new { name = "version", dataType = new[] { "text" }, tokenization = "field" },
                    new { name = "full_version_title", dataType = new[] { "text" }, tokenization = "field" },
                    new { name = "release_date", dataType = new[] { "date" } },
                    new { name = "metadata_note", dataType = new[] { "text" }, tokenization = "word" },
                    new { name = "section_header", dataType = new[] { "text" } },
                    new { name = "content_chunk", dataType = new[] { "text" } },
                    new { name = "ref_tickets", dataType = new[] { "text" }, tokenization = "word" }
                }
            };
        }

        /// <summary>
        /// Core abstract pipeline worker checking if an individual collection is active, dropping into REST deployment streams if missing.
        /// </summary>
        /// <param name="className">The case-sensitive schema collection class layout identity to query.</param>
        /// <param name="schema">The anonymous collection metadata properties payload configuration tree to establish.</param>
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
                throw;
            }
        }
    }
}