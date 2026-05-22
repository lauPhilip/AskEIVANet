using System.Collections.Generic;
using System.Threading.Tasks;
using AskEiva.Domain.Entities;

namespace AskEiva.Domain.Services;

public interface IReleaseNotesScraper
{
    /// <summary>
    /// Scrapes the EIVA download site, navigates product categories (NaviSuite, ROTV, ATTU),
    /// downloads active version release note PDFs, and chunks them into domain nodes.
    /// </summary>
    Task<IEnumerable<SoftwareReleaseNode>> ScrapeAndChunkAllReleaseNotesAsync();
}