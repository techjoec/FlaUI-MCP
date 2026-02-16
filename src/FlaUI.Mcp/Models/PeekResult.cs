// src/FlaUI.Mcp/Models/PeekResult.cs
using System.Text;
using System.Text.Json.Serialization;

namespace PlaywrightWindows.Mcp.Models;

/// <summary>
/// Result from windows_peek tool - lightweight element tree preview (~50 tokens)
/// </summary>
public class PeekResult : IToolResult
{
    [JsonPropertyName("ref")]
    public string Ref { get; set; } = "";

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("childCount")]
    public int ChildCount { get; set; }

    [JsonPropertyName("children")]
    public List<PeekElement> Children { get; set; } = new();

    public string ToCompactString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{Role} \"{Name}\" [{Ref}] - {ChildCount} children");

        foreach (var child in Children)
        {
            var disabled = !child.Enabled ? " [disabled]" : "";
            sb.AppendLine($"  [{child.Ref}] {child.Role} \"{child.Name}\"{disabled}");
        }

        return sb.ToString().TrimEnd();
    }

    public object ToStructuredData() => this;
}

public class PeekElement
{
    [JsonPropertyName("ref")]
    public string Ref { get; set; } = "";

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}
