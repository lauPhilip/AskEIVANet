using System.Collections.Generic;
using System.Threading.Tasks;
using AskEiva.Domain.Entities;

namespace AskEiva.Domain.Services;

public interface IDocumentationCrawler
{
    IAsyncEnumerable<DocumentationNode> CrawlSolutionsAsync(string categoryId);
}