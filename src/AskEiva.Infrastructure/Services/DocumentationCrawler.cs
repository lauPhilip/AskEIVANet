using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Services;
using HtmlAgilityPack;

namespace AskEiva.Infrastructure.Services;

public class DocumentationCrawler : IDocumentationCrawler
{
    private readonly HttpClient _httpClient;
    private const string BasePortalUrl = "https://eiva.freshdesk.com";

    public DocumentationCrawler(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

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

            // Normalize link structure pathways
            if (!href.StartsWith("http"))
            {
                href = href.StartsWith("/") ? $"{BasePortalUrl}{href}" : $"{BasePortalUrl}/{href}";
            }

            // Clean off tracking query strings to keep strings completely pristine
            if (href.Contains("?")) href = href.Split('?')[0];

            if (!uniquelyFoundUrls.Add(href)) continue; // Skip duplicates

            DocumentationNode? parsedNode = null;
            try
            {
                // Download the public facing webpage view assembly directly
                var articlePageDoc = await web.LoadFromWebAsync(href);
                var rootNode = articlePageDoc?.DocumentNode;
                if (rootNode == null) continue;

                // Target standard Freshdesk article layout container nodes safely
                var titleHeader = rootNode.SelectSingleNode("//h2[contains(@class, 'heading')]") 
                                  ?? rootNode.SelectSingleNode("//h1") 
                                  ?? rootNode.SelectSingleNode("//title");
                                  
                var bodyArticleContent = rootNode.SelectSingleNode("//div[contains(@class, 'article-body')]") 
                                         ?? rootNode.SelectSingleNode("//article") 
                                         ?? rootNode.SelectSingleNode("//body");

                if (bodyArticleContent != null)
                {
                    // 💡 FIXED CS0029: Explicit boolean null comparison check used here!
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

                    // EXTRACED CONTENT: Retain inner HTML formatting tags for the TextSplitter
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
                // Yield the rich document node into the stream instantly
                yield return parsedNode;
            }

            // Polite scraping interval pace gap 
            await Task.Delay(100);
        }
    }
}