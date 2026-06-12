using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Services;
using HtmlAgilityPack;

namespace AskEiva.Infrastructure.Services;

/// <summary>
/// Implements the <see cref="IDocumentationCrawler"/> domain contract using <see cref="HtmlAgilityPack"/> 
/// to crawl, scrape, parse, and yield public technical documentation assets from external helpdesk portals.
/// </summary>
public class DocumentationCrawler : IDocumentationCrawler
{
    private readonly HttpClient _httpClient;
    private const string BasePortalUrl = "https://eiva.freshdesk.com";

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentationCrawler"/> class with an explicit HTTP client.
    /// </summary>
    /// <param name="httpClient">The system infrastructure HTTP client factory instance used for routing targets.</param>
    public DocumentationCrawler(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Connects to the root public solution dashboard, parses available article link paths via XPath selectors, 
    /// and streams parsed, metadata-enriched documentation assets asynchronously.
    /// </summary>
    /// <param name="categoryId">The unique categorization filter key identifying the online section or folder tree to target.</param>
    /// <returns>An asynchronous stream yielding fully populated <see cref="DocumentationNode"/> entities as they are parsed.</returns>
    public async IAsyncEnumerable<DocumentationNode> CrawlSolutionsAsync(string categoryId)
    {
        Console.WriteLine("[Public Web Scraper] Initializing open web parsing pipeline against EIVA Helpdesk...");

        HtmlWeb web = new HtmlWeb();
        HtmlDocument sitemapDoc;

        try
        {
            // Load the primary root public solutions dashboard view layout
            sitemapDoc = await web.LoadFromWebAsync($"{BasePortalUrl}/support/solutions");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Scraper Critical Error] Failed to load baseline portal index site: {ex.Message}");
            yield break;
        }

        if (sitemapDoc?.DocumentNode == null) yield break;

        // Target all public article solution anchor hyperlinks on the directory page
        var articleLinks = sitemapDoc.DocumentNode.SelectNodes("//a[contains(@href, '/support/solutions/articles/')]");

        if (articleLinks == null)
        {
            Console.WriteLine("⚠️ [Scraper Warning] Could not find any public article hyperlinks on the landing page layout index.");
            yield break;
        }

        var uniquelyFoundUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var linkNode in articleLinks)
        {
            string href = linkNode.GetAttributeValue("href", string.Empty);
            if (string.IsNullOrWhiteSpace(href)) continue;

            // Normalize link structure pathways from relative to absolute address chains
            if (!href.StartsWith("http"))
            {
                href = href.StartsWith("/") ? $"{BasePortalUrl}{href}" : $"{BasePortalUrl}/{href}";
            }

            // Clean off tracking query strings to keep URLs completely pristine
            if (href.Contains('?')) href = href.Split('?')[0];

            if (!uniquelyFoundUrls.Add(href)) continue; // Skip duplicate targets

            DocumentationNode? parsedNode = null;
            try
            {
                // Download the public facing webpage view assembly directly
                var articlePageDoc = await web.LoadFromWebAsync(href);
                var rootNode = articlePageDoc?.DocumentNode;
                if (rootNode == null) continue;

                // Target standard Freshdesk article layout container nodes safely using cascading selector fallbacks
                var titleHeader = rootNode.SelectSingleNode("//h2[contains(@class, 'heading')]") 
                                  ?? rootNode.SelectSingleNode("//h1") 
                                  ?? rootNode.SelectSingleNode("//title");
                                  
                var bodyArticleContent = rootNode.SelectSingleNode("//div[contains(@class, 'article-body')]") 
                                         ?? rootNode.SelectSingleNode("//article") 
                                         ?? rootNode.SelectSingleNode("//body");

                if (bodyArticleContent != null)
                {
                    // Fixed compiler CS0029 error block using explicit boolean null validation check
                    string cleanTitle = titleHeader != null ? titleHeader.InnerText.Trim() : "EIVA Reference Guide";
                    
                    // Extract any raw PDF attachment elements present on the page context 
                    var pdfUrls = new List<string>();
                    var attachmentNodes = bodyArticleContent.SelectNodes("//a[contains(@href, '.pdf') or contains(@href, '/attachments/')]");
                    if (attachmentNodes != null)
                    {
                        foreach (var attach in attachmentNodes)
                        {
                            string targetUrl = attach.GetAttributeValue("href", string.Empty);
                            if (!string.IsNullOrEmpty(targetUrl)) pdfUrls.Add(targetUrl);
                        }
                    }

                    // RETAIN INNER HTML: Keep raw structural tags (like img markers) intact so the TextSplitter utility can parse them
                    string fullContentHtml = bodyArticleContent.InnerHtml;
                    string extractedId = href.Split('/').LastOrDefault() ?? Guid.NewGuid().ToString();

                    parsedNode = new DocumentationNode
                    {
                        SourceId = $"kb_{extractedId}",
                        Title = cleanTitle,
                        Category = "Public Manual Asset",
                        Content = fullContentHtml,
                        SourceUrl = href,
                        AssociatedPdfUrls = pdfUrls,
                        CrawledAt = DateTime.UtcNow
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ [Scraper Field Drop] Bypassing target guide page layout [ {href} ]: {ex.Message}");
            }

            if (parsedNode != null)
            {
                // Yield the rich document node into the asynchronous data stream instantly
                yield return parsedNode;
            }

            // Polite scraping interval pace gap to protect external network infrastructure thresholds
            await Task.Delay(100);
        }
    }
}