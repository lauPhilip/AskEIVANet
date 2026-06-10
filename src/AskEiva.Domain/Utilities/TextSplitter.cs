using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AskEiva.Domain.ValueObjects;
using AskEiva.Domain.Entities;

namespace AskEiva.Domain.Utilities;

public class TextSplitter
{
    private readonly int _chunkSize;
    private readonly int _chunkOverlap;
    
    // Regex to extract image source links from Freshdesk HTML content
    private static readonly Regex ImageRegex = new(@"<img[^>]+src=[""']([^""']+)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public TextSplitter(int chunkSize = 1000, int chunkOverlap = 200)
    {
        _chunkSize = chunkSize;
        _chunkOverlap = chunkOverlap;
    }

    /// <summary>
    /// Slices historic customer support tickets into vectorized chunks (Unchanged)
    /// </summary>
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

            if (position + length < cleanText.Length)
            {
                int lastSeparator = content.LastIndexOfAny(new[] { '.', '\n', ' ' });
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
            position += (length - _chunkOverlap) > 0 ? (length - _chunkOverlap) : length;
        }

        return chunks;
    }

    /// <summary>
    /// 💡 NEW OVERLOAD: Explicitly tailored to handle dense technical EIVA User Manuals and Guides 
    /// without falling into sentence-punctuation parsing traps.
    /// </summary>
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