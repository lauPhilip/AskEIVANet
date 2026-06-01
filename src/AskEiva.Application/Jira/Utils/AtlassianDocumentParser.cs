using System;
using System.Collections.Generic;
using System.Text.Json;

namespace AskEiva.Application.Jira.Utils;

public static class AtlassianDocumentParser
{
    public static string ToPlainText(object? adfObject)
    {
        if (adfObject == null) return string.Empty;
        if (adfObject is JsonElement jsonElement) return ExtractText(jsonElement);

        try
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(adfObject));
            return ExtractText(doc.RootElement);
        }
        catch { return string.Empty; }
    }

    private static string ExtractText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
            return string.Empty;

        if (element.ValueKind == JsonValueKind.String)
            return element.GetString() ?? string.Empty;

        if (element.ValueKind == JsonValueKind.Object && 
            element.TryGetProperty("type", out var typeProp) && 
            typeProp.GetString() == "text" && 
            element.TryGetProperty("text", out var textProp))
        {
            return textProp.GetString() ?? string.Empty;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            var textPieces = new List<string>();
            foreach (var child in element.EnumerateArray()) textPieces.Add(ExtractText(child));
            return string.Join("", textPieces);
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            var result = string.Empty;
            if (element.TryGetProperty("content", out var contentProp)) result = ExtractText(contentProp);

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