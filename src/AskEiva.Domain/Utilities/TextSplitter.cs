using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AskEiva.Domain.ValueObjects;
using AskEiva.Domain.Entities;

namespace AskEiva.Domain.Utilities;

/// <summary>
/// Provides high-efficiency sliding-window text segmentation utilities to sanitize HTML strings, 
/// extract image targets, and slice continuous bodies into size-bounded vector text chunks.
/// </summary>
public class TextSplitter
{
    private readonly int _chunkSize;
    private readonly int _chunkOverlap;
    
    /// <summary>
    /// Compiled, case-insensitive regular expression used to discover and isolate image resource URLs from HTML tags.
    /// </summary>
    private static readonly Regex ImageRegex = new(@"<img[^>]+src=[""']([^""']+)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Initializes a new instance of the text splitter configuration with customized character count sizes and overlap windows.
    /// </summary>
    /// <param name="chunkSize">The absolute maximum length of characters allowed within an individual slice (defaults to 1000).</param>
    /// <param name="chunkOverlap">The character buffer size duplicated between consecutive segments to maintain sentence semantic context (defaults to 200).</param>
    public TextSplitter(int chunkSize = 1000, int chunkOverlap = 200)
    {
        _chunkSize = chunkSize;
        _chunkOverlap = chunkOverlap;
    }

    /// <summary>
    /// Slices raw historical customer support ticket conversation loops into formatted, overlapping token chunks.
    /// </summary>
    /// <param name="ticket">The parent support ticket node entity containing raw discussion log data.</param>
    /// <returns>A collection sequence of individual, sequence-tracked text chunk value objects.</returns>
    public IEnumerable<TextChunk> SplitTicket(TicketNode ticket)
    {
        var chunks = new List<TextChunk>();
        var imageUrls = ExtractImageUrls(ticket.Content);
        var cleanText = CleanHtml(ticket.Content);
        
        if (string.IsNullOrWhiteSpace(cleanText))
            return chunks;

        int position = 0;
        int sequence = 0;

        while (position < cleanText.Length)
        {
            int length = Math.Min(_chunkSize, cleanText.Length - position);
            var content = cleanText.Substring(position, length);

            // Adjust boundary split point if we aren't evaluating the absolute end of the document
            if (position + length < cleanText.Length)
            {
                int lastSeparator = content.LastIndexOfAny(new[] { '.', '\n', ' ' });
                
                // Truncate length smoothly up to a valid natural word or punctuation separator point
                if (lastSeparator > _chunkSize * 0.5) 
                {
                    length = lastSeparator + 1;
                    content = cleanText.Substring(position, length);
                }
            }

            var metadata = new Dictionary<string, string>
            {
                { "Subject", ticket.Subject },
                { "Url", ticket.Url },
                { "Status", ticket.Status.ToString() }
            };

            chunks.Add(new TextChunk(
                ChunkId: $"{ticket.SourceId}_ch_{sequence}",
                SourceId: ticket.SourceId,
                Content: content.Trim(),
                SequenceNumber: sequence,
                ImageUrls: imageUrls, 
                Metadata: metadata
            ));

            sequence++;
            
            // Step forward by the exact segment length minus the predefined overlap buffer
            position += (length - _chunkOverlap) > 0 ? (length - _chunkOverlap) : length;
        }

        return chunks;
    }

    /// <summary>
    /// Slices dense technical engineering manuals and guides into size-bounded vector text chunks 
    /// without splitting technical terms or words mid-sentence.
    /// </summary>
    /// <param name="doc">The documentation node entity containing technical data and manual records.</param>
    /// <returns>A collection sequence of formatted, metadata-enriched text chunk value objects.</returns>
    public IEnumerable<TextChunk> SplitDocumentation(DocumentationNode doc)
    {
        var chunks = new List<TextChunk>();
        
        if (doc == null || string.IsNullOrWhiteSpace(doc.Content))
            return chunks;

        // 1. Extract image locations embedded right inside this user manual entry
        var imageUrls = ExtractImageUrls(doc.Content);

        // 2. Decode web markup expressions and strip out the noise
        var cleanText = CleanHtml(doc.Content);
        
        if (string.IsNullOrWhiteSpace(cleanText))
            return chunks;

        int position = 0;
        int sequence = 0;

        // 3. Sliding Window Strategy
        while (position < cleanText.Length)
        {
            int length = Math.Min(_chunkSize, cleanText.Length - position);
            var content = cleanText.Substring(position, length);

            // Refine split window boundary to prevent mid-word layout splitting if progress allows
            if (position + length < cleanText.Length)
            {
                int lastSeparator = content.LastIndexOfAny(new[] { ' ', '\n', '.' });
                
                // Fallback barrier check: only shorten the slice if we maintain a reasonable density threshold
                if (lastSeparator > _chunkSize * 0.6) 
                {
                    length = lastSeparator + 1;
                    content = cleanText.Substring(position, length);
                }
            }

            var metadata = new Dictionary<string, string>
            {
                { "Subject", doc.Title },
                { "Url", doc.SourceUrl },
                { "Status", "ManualBaseline" }
            };

            chunks.Add(new TextChunk(
                ChunkId: $"{doc.SourceId}_part{sequence}",
                SourceId: doc.SourceId,
                Content: content.Trim(),
                SequenceNumber: sequence,
                ImageUrls: imageUrls, 
                Metadata: metadata
            ));

            sequence++;

            // Force a progressive window step forwards
            int advanceStep = length - _chunkOverlap;
            position += (advanceStep > 0) ? advanceStep : length;
        }

        return chunks;
    }

    /// <summary>
    /// Evaluates raw source strings with compiled regular expressions to discover embedded image web sources.
    /// </summary>
    /// <param name="htmlContent">The uncleaned text or HTML markup structure containing possible image source elements.</param>
    /// <returns>A list of clean uniform resource identifiers mapping image links.</returns>
    private List<string> ExtractImageUrls(string htmlContent)
    {
        var urls = new List<string>();
        if (string.IsNullOrEmpty(htmlContent)) return urls;

        var matches = ImageRegex.Matches(htmlContent);
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                urls.Add(match.Groups[1].Value);
            }
        }
        return urls;
    }

    /// <summary>
    /// Decodes HTML syntax structures, maps break tags to literal platform structural layout elements, 
    /// and flattens whitespace clusters to maximize database indexing efficiency.
    /// </summary>
    /// <param name="htmlContent">The raw unparsed string extracted from documentation pipelines or ticket APIs.</param>
    /// <returns>A completely flat, plain text string free of markup tags and trailing spaces.</returns>
    private string CleanHtml(string htmlContent)
    {
        if (string.IsNullOrEmpty(htmlContent)) return string.Empty;

        try
        {
            // Decodes HTML entities (&lt;, &gt;, &amp;, etc.) back into clean literal punctuation markers
            string decodedHtml = System.Net.WebUtility.HtmlDecode(htmlContent);

            // Standardize spaces and break tags into structural newlines
            string text = decodedHtml
                .Replace("<br>", "\n")
                .Replace("<br/>", "\n")
                .Replace("<br />", "\n")
                .Replace("</p>", "\n\n")
                .Replace("</div>", "\n");

            // Strip out all the remaining HTML brackets cleanly
            string cleanText = Regex.Replace(text, "<[^>]*>", string.Empty);

            // Clean up running whitespace clusters to maximize token efficiency inside Weaviate
            return Regex.Replace(cleanText, @"[ \t]+", " ");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TextSplitter HTML Parser Error Block]: {ex.Message}");
            return string.Empty;
        }
    }
}