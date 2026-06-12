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
using AskEiva.Domain.Repositories;
using UglyToad.PdfPig;

namespace AskEiva.Infrastructure.Services;

/// <summary>
/// Implements the <see cref="IReleaseNotesScraper"/> contract by parsing a local manifest index mapping file, 
/// resolving an idempotency cache gate against Weaviate, downloading target changelog PDFs, 
/// and extraction-slicing text streams into structured version entities using PdfPig.
/// </summary>
public class ReleaseNotesScraper : IReleaseNotesScraper
{
    private readonly HttpClient _httpClient;
    private readonly IKnowledgeRetrievalRepository _knowledgeRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReleaseNotesScraper"/> class, leveraging 
    /// a dual-dependency design to handle network streams and persistence verification steps.
    /// </summary>
    /// <param name="httpClient">An unmanaged or factory-allocated HTTP client configured to target document servers.</param>
    /// <param name="knowledgeRepository">The underlying vector store repository used to check entry existence flags.</param>
    public ReleaseNotesScraper(HttpClient httpClient, IKnowledgeRetrievalRepository knowledgeRepository)
    {
        _httpClient = httpClient;
        _knowledgeRepository = knowledgeRepository;
    }

    /// <summary>
    /// Evaluates the local release log manifest file, checks version processing statuses defensively, 
    /// pulls remote PDF streams via a modified URL escape routing layer, and yields chunk data blocks.
    /// </summary>
    /// <returns>A collection stream loaded with populated, structural software release node entities.</returns>
    public async Task<IEnumerable<SoftwareReleaseNode>> ScrapeAndChunkAllReleaseNotesAsync()
    {
        var globalDiscoveredNodes = new List<SoftwareReleaseNode>();
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
                            string explicitVersion = GetCleanVersionString(target.Version);
                            
                            // THE IDEMPOTENCY GATE: Check if Weaviate already knows about this combination
                            if (await _knowledgeRepository.DoesProductVersionExistAsync(product.Name, explicitVersion))
                            {
                                Console.WriteLine($"[Scraper Guard] Track skipped: {product.Name} v{explicitVersion} is already indexed and up to date.");
                                continue;
                            }

                            var nodes = await ProcessSinglePdfUrlTrackAsync(target, category.Name, product.Name, "Latest");
                            globalDiscoveredNodes.AddRange(nodes);
                        }
                    }

                    // 2. Process the "Archive" legacy list track
                    if (product.Archive != null)
                    {
                        foreach (var target in product.Archive)
                        {
                            string explicitVersion = GetCleanVersionString(target.Version);

                            // THE IDEMPOTENCY GATE: Check if Weaviate already knows about this combination
                            if (await _knowledgeRepository.DoesProductVersionExistAsync(product.Name, explicitVersion))
                            {
                                Console.WriteLine($"[Scraper Guard] Track skipped: {product.Name} v{explicitVersion} is already indexed and up to date.");
                                continue;
                            }

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

    /// <summary>
    /// Parses raw version tags to isolate clean alphanumeric core labels clear of hyphens or whitespace artifacts.
    /// </summary>
    private string GetCleanVersionString(string rawVersion)
    {
        string version = rawVersion;
        if (version.Contains('-')) version = version.Split('-')[1].Trim();
        if (version.Contains('–')) version = version.Split('–')[1].Trim();
        return version;
    }

    /// <summary>
    /// Downloads an individual release note document payload over the network, resolving spaces and raw strings 
    /// defensively before unpacking binary structures via PdfPig tracking models.
    /// </summary>
    private async Task<List<SoftwareReleaseNode>> ProcessSinglePdfUrlTrackAsync(VersionEntryDto target, string categoryName, string productName, string releaseType)
    {
        var fileChunks = new List<SoftwareReleaseNode>();
        string explicitVersion = GetCleanVersionString(target.Version);
        string fullTitle = $"{productName} – {explicitVersion}";

        // SCENARIO A: Standalone Ingestion Case (No PDF provided)
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
                Console.WriteLine($"[Scraper Note Sync] Processed standalone manifest footnote metadata for: {fullTitle}");
            }
            return fileChunks;
        }

        // SCENARIO B: Full Document Download Sequence
        try
        {
            string safeRelativePath = target.RelativeUrl;
            if (safeRelativePath.StartsWith("https://download.eiva.com/", StringComparison.OrdinalIgnoreCase))
            {
                safeRelativePath = safeRelativePath.Replace("https://download.eiva.com/", "", StringComparison.OrdinalIgnoreCase);
            }

            if (safeRelativePath.Contains(' '))
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

    /// <summary>
    /// Utility method using stream buffers to extract flat character layers out of byte collections using sequential page layouts.
    /// </summary>
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

    /// <summary>
    /// Parses extracted raw page content layers, using pre-compiled regular expressions to isolate individual tracking sections 
    /// or capturing issue tickets dynamically before dropping back into a generic token sliding window strategy if unformatted.
    /// </summary>
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

        if (!chunks.Any())
        {
            var ticketMatches = ticketRegex.Matches(pageText);
            var ticketsList = ticketMatches.Cast<Match>().Select(m => m.Groups[1].Value.Trim()).Distinct();
            string joinedTickets = string.Join(" ", ticketsList);

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
                var words = pageText.Split([ ' ', '\r', '\n' ], StringSplitOptions.RemoveEmptyEntries);
                int chunkSize = 400;
                int overlap = 50;

                for (int i = 0; i < words.Length; i += (chunkSize - overlap))
                {
                    var chunkWords = words.Skip(i).Take(chunkSize).ToList();
                    if (chunkWords.Count < 20) break;

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

    // --- MANIFEST STRUCTURE TRANSPORT DATA OBJECT DIAGRAMS ---

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