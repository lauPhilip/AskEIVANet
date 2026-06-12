using System.Collections.Generic;
using System.Threading.Tasks;
using AskEiva.Domain.Entities;

namespace AskEiva.Domain.Services;

/// <summary>
/// Defines the external web crawling service contracts tasked with connecting to online document resources 
/// and scraping engineering resolution pages.
/// </summary>
public interface IDocumentationCrawler
{
    /// <summary>
    /// Connects to a target repository segment and crawls support articles, pulling documentation models back via an asynchronous data stream.
    /// </summary>
    /// <param name="categoryId">The unique categorization key identifying the online section or folder tree to target.</param>
    /// <returns>An asynchronous collection stream that yields populated documentation node entities as they are parsed from the web.</returns>
    IAsyncEnumerable<DocumentationNode> CrawlSolutionsAsync(string categoryId);
}