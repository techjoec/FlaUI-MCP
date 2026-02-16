// src/FlaUI.Mcp/Models/FindResult.cs
using System.Text;
using System.Text.Json.Serialization;

namespace PlaywrightWindows.Mcp.Models;

/// <summary>
/// Result from windows_find tool - scoped element search (~100-200 tokens)
/// </summary>
public class FindResult : IToolResult
{
    [JsonPropertyName("query")]
    public FindQuery Query { get; set; } = new();

    [JsonPropertyName("totalSearched")]
    public int TotalSearched { get; set; }

    [JsonPropertyName("matchCount")]
    public int MatchCount { get; set; }

    [JsonPropertyName("truncated")]
    public bool Truncated { get; set; }

    [JsonPropertyName("elements")]
    public List<FindResultElement> Elements { get; set; } = new();

    public string ToCompactString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Found {MatchCount} {Query.Role}s");

        foreach (var e in Elements)
        {
            var disabled = !e.Enabled ? " [disabled]" : "";
            sb.AppendLine($"  [{e.Ref}] \"{e.Name}\"{disabled}");
        }

        if (Truncated)
            sb.AppendLine($"[Search truncated at {TotalSearched} elements]");

        return sb.ToString().TrimEnd();
    }

    public object ToStructuredData() => this;
}

public class FindQuery
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("nameContains")]
    public string? NameContains { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }
}

public class FindResultElement
{
    [JsonPropertyName("ref")]
    public string Ref { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}
