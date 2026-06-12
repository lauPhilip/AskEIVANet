using System;
using System.Collections.Generic;
using System.Text.Json;

namespace AskEiva.Application.Jira.Utils;

/// <summary>
/// A utility parsing class designed to traverse and extract plain text from complex, nested Atlassian Document Format (ADF) objects.
/// </summary>
public static class AtlassianDocumentParser
{
    /// <summary>
    /// Evaluates an unexpected input object and safely extracts its plain-text representation.
    /// </summary>
    /// <param name="adfObject">The raw object or JSON element representing the Atlassian Document Format tree structure.</param>
    /// <returns>A clean string containing the flattened text content, or an empty string if parsing fails.</returns>
    public static string ToPlainText(object? adfObject)
    {
        if (adfObject == null) return string.Empty;
        if (adfObject is JsonElement jsonElement) return ExtractText(jsonElement);

        try
        {
            // Fallback parsing strategy if the object is passed as an unstructured type instead of a typed JsonElement
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(adfObject));
            return ExtractText(doc.RootElement);
        }
        catch 
        { 
            return string.Empty; 
        }
    }

    /// <summary>
    /// Recursively processes individual JSON nodes to extract text components and manage layout structures like newlines.
    /// </summary>
    /// <param name="element">The specific JSON token node currently under evaluation in the tree structure.</param>
    /// <returns>The accumulated plain-text string extracted from the node and all of its child elements.</returns>
    private static string ExtractText(JsonElement element)
    {
        // Safe exit check for empty or uninitialized tokens
        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
            return string.Empty;

        // Directly return standard string values
        if (element.ValueKind == JsonValueKind.String)
            return element.GetString() ?? string.Empty;

        // Base Case: Extract text property value if the current node represents a leaf text element
        if (element.ValueKind == JsonValueKind.Object && 
            element.TryGetProperty("type", out var typeProp) && 
            typeProp.GetString() == "text" && 
            element.TryGetProperty("text", out var textProp))
        {
            return textProp.GetString() ?? string.Empty;
        }

        // Recursive Pass: Enumerate all indices within an array node and combine their text strings
        if (element.ValueKind == JsonValueKind.Array)
        {
            var textPieces = new List<string>();
            foreach (var child in element.EnumerateArray()) 
            {
                textPieces.Add(ExtractText(child));
            }
            return string.Join("", textPieces);
        }

        // Recursive Pass: Evaluate interior object nodes by drilling directly down into their nested content blocks
        if (element.ValueKind == JsonValueKind.Object)
        {
            var result = string.Empty;
            if (element.TryGetProperty("content", out var contentProp)) 
            {
                result = ExtractText(contentProp);
            }

            // Append structural line breaks if the object container signifies a text paragraph boundary or block title
            if (element.TryGetProperty("type", out var tProp) && 
                (tProp.GetString() == "paragraph" || tProp.GetString() == "heading"))
            {
                result += Environment.NewLine;
            }
            return result;
        }
        
        return string.Empty;
    }
}