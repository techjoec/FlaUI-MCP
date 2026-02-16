// src/FlaUI.Mcp/Models/ReadResult.cs
using System.Text;
using System.Text.Json.Serialization;

namespace PlaywrightWindows.Mcp.Models;

/// <summary>
/// Result from windows_read tool - element property inspection (~100 tokens)
/// </summary>
public class ReadResult : IToolResult
{
    [JsonPropertyName("ref")]
    public string Ref { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("visible")]
    public bool Visible { get; set; } = true;

    [JsonPropertyName("focused")]
    public bool Focused { get; set; }

    [JsonPropertyName("bounds")]
    public string? Bounds { get; set; }

    [JsonPropertyName("patterns")]
    public List<string> Patterns { get; set; } = new();

    public string ToCompactString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{Ref}] {Role} \"{Name}\"");

        if (!string.IsNullOrEmpty(Value))
            sb.AppendLine($"Value: {Value}");

        if (!Enabled)
            sb.Append("[disabled] ");
        if (!Visible)
            sb.Append("[hidden] ");
        if (Focused)
            sb.Append("[focused] ");

        if (sb.Length > 0 && sb[^1] == ' ')
            sb.Length--; // Remove trailing space

        return sb.ToString().TrimEnd();
    }

    public object ToStructuredData() => this;
}
