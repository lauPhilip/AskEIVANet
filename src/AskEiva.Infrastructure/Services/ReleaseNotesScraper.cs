using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AskEiva.Domain.Entities;
using AskEiva.Domain.Services;
using UglyToad.PdfPig;

namespace AskEiva.Infrastructure.Services;

public class ReleaseNotesScraper : IReleaseNotesScraper
{
    private readonly HttpClient _httpClient;

    public ReleaseNotesScraper(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<SoftwareReleaseNode>> ScrapeAndChunkAllReleaseNotesAsync()
    {
        var globalDiscoveredNodes = new List<SoftwareReleaseNode>();
        
        // Locate the centralized structural manifest layout document
        string manifestPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "release_notes_manifest.json");

        if (!File.Exists(manifestPath))
        {
            Console.WriteLine($"[Scraper Configuration Error] Manifest document not located at: {manifestPath}");
            return globalDiscoveredNodes;
        }

        try
        {
            string jsonContent = await File.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<ReleaseNotesManifestDto>(jsonContent, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (manifest?.Categories == null) return globalDiscoveredNodes;

            foreach (var category in manifest.Categories)
            {
                if (category.Products == null) continue;

                foreach (var product in category.Products)
                {
                    // 1. Process the "Latest" release list track
                    if (product.Latest != null)
                    {
                        foreach (var target in product.Latest)
                        {
                            var nodes = await ProcessSinglePdfUrlTrackAsync(target, category.Name, product.Name, "Latest");
                            globalDiscoveredNodes.AddRange(nodes);
                        }
                    }

                    // 2. Process the "Archive" legacy list track
                    if (product.Archive != null)
                    {
                        foreach (var target in product.Archive)
                        {
                            var nodes = await ProcessSinglePdfUrlTrackAsync(target, category.Name, product.Name, "Archive");
                            globalDiscoveredNodes.AddRange(nodes);
                        }
                    }
                }
            }
        }
        catch (Exception globalEx)
        {
            Console.WriteLine($"[Scraper Runtime Crash] Nested manifest execution loop collapsed: {globalEx.Message}");
        }

        return globalDiscoveredNodes;
    }

    private async Task<List<SoftwareReleaseNode>> ProcessSinglePdfUrlTrackAsync(VersionEntryDto target, string categoryName, string productName, string releaseType)
    {
        var fileChunks = new List<SoftwareReleaseNode>();

        // Clean up titles and decouple version keys cleanly
        string explicitVersion = target.Version;
        if (explicitVersion.Contains("-")) explicitVersion = explicitVersion.Split('-')[1].Trim();
        if (explicitVersion.Contains("–")) explicitVersion = explicitVersion.Split('–')[1].Trim();
        string fullTitle = $"{productName} – {explicitVersion}";

        // 💡 SCENARIO A: Standalone Ingestion Case (No PDF provided, parse metadata note text directly)
        if (string.IsNullOrWhiteSpace(target.RelativeUrl))
        {
            if (!string.IsNullOrWhiteSpace(target.Note))
            {
                fileChunks.Add(new SoftwareReleaseNode
                {
                    GroupCategory = categoryName,
                    Product = productName,
                    ReleaseType = releaseType,
                    Version = explicitVersion,
                    FullVersionTitle = fullTitle,
                    MetadataNote = target.Note,
                    ReleaseDate = target.ReleaseDate,
                    SectionHeader = "Deployment Dependency & Compatibility Notice",
                    ContentChunk = target.Note,
                    RefTickets = string.Empty
                });
                Console.WriteLine($"[Scraper Note Sync] Process Processed standalone manifest footnote metadata for: {fullTitle}");
            }
            return fileChunks;
        }

        // 💡 SCENARIO B: Full Document Download Sequence & Robust Page Processing
        try
        {
            string safeRelativePath = target.RelativeUrl;
            if (safeRelativePath.StartsWith("https://download.eiva.com/", StringComparison.OrdinalIgnoreCase))
            {
                safeRelativePath = safeRelativePath.Replace("https://download.eiva.com/", "", StringComparison.OrdinalIgnoreCase);
            }

            if (safeRelativePath.Contains(" "))
            {
                var segments = safeRelativePath.Split('/');
                for (int i = 0; i < segments.Length; i++)
                {
                    segments[i] = Uri.EscapeDataString(Uri.UnescapeDataString(segments[i]));
                }
                safeRelativePath = string.Join("/", segments);
            }

            Console.WriteLine($"[Scraper Network Stream] Requesting [{releaseType}] URI path: {safeRelativePath}");
            byte[] downloadedPdfBytes = await _httpClient.GetByteArrayAsync(safeRelativePath);
            
            // 💡 OPTIMIZATION: Process page-by-page directly instead of flattening to a single giant string
            using (var memoryStream = new MemoryStream(downloadedPdfBytes))
            using (var document = PdfDocument.Open(memoryStream))
            {
                int pageNumber = 1;
                foreach (var page in document.GetPages())
                {
                    string pageText = page.Text;
                    if (string.IsNullOrWhiteSpace(pageText) || pageText.Length < 20)
                    {
                        pageNumber++;
                        continue; 
                    }

                    // Parse this concrete page's text entries safely via the robust page partition system
                    var pageChunks = ChunkSinglePageContent(
                        pageText, 
                        pageNumber,
                        categoryName, 
                        productName, 
                        releaseType, 
                        explicitVersion, 
                        fullTitle, 
                        target.Note, 
                        target.ReleaseDate
                    );

                    fileChunks.AddRange(pageChunks);
                    pageNumber++;
                }
            }

            Console.WriteLine($"[Scraper Index Task] Successfully structured target record: {fullTitle} ({fileChunks.Count} total semantic chunks generated across all pages).");
        }
        catch (Exception assetEx)
        {
            Console.WriteLine($"[Scraper Exception] Skipped problematic asset file channel [{target.RelativeUrl}]: {assetEx.Message}");
        }

        return fileChunks;
    }

    private string ExtractTextFromPdfBytes(byte[] pdfBytes)
    {
        var sb = new StringBuilder();
        
        using (var memoryStream = new MemoryStream(pdfBytes))
        using (var document = PdfDocument.Open(memoryStream))
        {
            foreach (var page in document.GetPages())
            {
                sb.AppendLine(page.Text);
            }
        }

        return sb.ToString();
    }

    private List<SoftwareReleaseNode> ChunkSinglePageContent(
        string pageText, 
        int pageNumber,
        string groupCategory, 
        string product, 
        string releaseType, 
        string version, 
        string fullVersionTitle, 
        string metadataNote,
        DateTime date)
    {
        var chunks = new List<SoftwareReleaseNode>();
        
        // Matches subsection headings on this specific page context
        var sectionRegex = new Regex(@"(?<header>\d+\.\d+\.\d+\s+[A-Za-z0-9\s\(\)\.\-\/]+)\r?\n(?<content>.*?)(?=\d+\.\d+\.\d+\s+[A-Za-z0-9\s\(\)\.\-\/]+|\z)", RegexOptions.Singleline);
        var ticketRegex = new Regex(@"\[(?:FD|J|DO)?-?(\d+)\]", RegexOptions.IgnoreCase);

        var matches = sectionRegex.Matches(pageText);
        foreach (Match match in matches)
        {
            string header = match.Groups["header"].Value.Trim();
            string content = match.Groups["content"].Value.Trim();

            if (string.IsNullOrWhiteSpace(content) || content.Length < 30 || header.Contains("Contents")) continue;

            var ticketMatches = ticketRegex.Matches(content);
            var ticketsList = ticketMatches.Cast<Match>().Select(m => m.Groups[1].Value.Trim()).Distinct();
            string joinedTickets = string.Join(" ", ticketsList);

            chunks.Add(new SoftwareReleaseNode
            {
                GroupCategory = groupCategory,
                Product = product,
                ReleaseType = releaseType,
                Version = version,
                FullVersionTitle = fullVersionTitle,
                MetadataNote = metadataNote,
                ReleaseDate = date,
                SectionHeader = $"{header} (Page {pageNumber})",
                ContentChunk = content,
                RefTickets = joinedTickets
            });
        }

        // 💡 DEFENSIVE FALLBACK PARSER: If a page doesn't match our regex headers, 
        // partition the raw page text by words to guarantee zero text loss.
        if (!chunks.Any())
        {
            var ticketMatches = ticketRegex.Matches(pageText);
            var ticketsList = ticketMatches.Cast<Match>().Select(m => m.Groups[1].Value.Trim()).Distinct();
            string joinedTickets = string.Join(" ", ticketsList);

            // If the page is relatively short, commit it as a single coherent block
            if (pageText.Length <= 2500)
            {
                chunks.Add(new SoftwareReleaseNode
                {
                    GroupCategory = groupCategory,
                    Product = product,
                    ReleaseType = releaseType,
                    Version = version,
                    FullVersionTitle = fullVersionTitle,
                    MetadataNote = metadataNote,
                    ReleaseDate = date,
                    SectionHeader = $"General Specifications Overview - Page {pageNumber}",
                    ContentChunk = pageText.Trim(),
                    RefTickets = joinedTickets
                });
            }
            else
            {
                // If it's a massive page layout data block, split it into 400-word blocks with a 50-word overlap window
                var words = pageText.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                int chunkSize = 400;
                int overlap = 50;

                for (int i = 0; i < words.Length; i += (chunkSize - overlap))
                {
                    var chunkWords = words.Skip(i).Take(chunkSize).ToList();
                    if (chunkWords.Count < 20) break; // Skip tiny dangling sentence remnants

                    string syntheticChunkText = string.Join(" ", chunkWords);

                    chunks.Add(new SoftwareReleaseNode
                    {
                        GroupCategory = groupCategory,
                        Product = product,
                        ReleaseType = releaseType,
                        Version = version,
                        FullVersionTitle = fullVersionTitle,
                        MetadataNote = metadataNote,
                        ReleaseDate = date,
                        SectionHeader = $"General Specifications Overview - Page {pageNumber} (Part {i / (chunkSize - overlap) + 1})",
                        ContentChunk = syntheticChunkText,
                        RefTickets = joinedTickets
                    });
                }
            }
        }

        return chunks;
    }

    // --- Local Strongly-Typed Mapping Data Transfer Objects ---
    private class ReleaseNotesManifestDto { public List<CategoryDto>? Categories { get; set; } }
    private class CategoryDto { public string Name { get; set; } = string.Empty; public List<ProductDto>? Products { get; set; } }
    private class ProductDto 
    { 
        public string Name { get; set; } = string.Empty; 
        public List<VersionEntryDto>? Latest { get; set; } 
        public List<VersionEntryDto>? Archive { get; set; } 
    }
    private class VersionEntryDto 
    { 
        public string Version { get; set; } = string.Empty; 
        public DateTime ReleaseDate { get; set; } 
        public string RelativeUrl { get; set; } = string.Empty; 
        public string Note { get; set; } = string.Empty; 
    }
}