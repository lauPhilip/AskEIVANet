using System.Collections.Generic;
using System.Threading.Tasks;
using AskEiva.Domain.Entities;

namespace AskEiva.Domain.Services;

/// <summary>
/// Defines the external web scraping and PDF parsing service contracts tasked with extracting, 
/// processing, and chunking software release notes from corporate distribution repositories.
/// </summary>
public interface IReleaseNotesScraper
{
    /// <summary>
    /// Navigates across corporate software product categories, extracts active version release logs, 
    /// processes PDF content arrays, and slices them into manageable text chunk metadata blocks.
    /// </summary>
    /// <returns>A collection sequence of individual software release node entities parsed and ready for indexing.</returns>
    Task<IEnumerable<SoftwareReleaseNode>> ScrapeAndChunkAllReleaseNotesAsync();
}