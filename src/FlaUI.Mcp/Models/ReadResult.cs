// src/FlaUI.Mcp/Models/ReadResult.cs
using System.Text;
using System.Text.Json.Serialization;

namespace PlaywrightWindows.Mcp.Models;

/// <summary>
/// Result from windows_read tool - region-scoped UI snapshot (~100-200 tokens)
/// </summary>
public class ReadResult : IToolResult
{
    [JsonPropertyName("region")]
    public string Region { get; set; } = "";

    [JsonPropertyName("found")]
    public bool Found { get; set; }

    [JsonPropertyName("elementCount")]
    public int ElementCount { get; set; }

    [JsonPropertyName("elements")]
    public List<ReadElement> Elements { get; set; } = new();

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    public string ToCompactString()
    {
        if (!Found)
            return $"Region '{Region}': not found. {Message}";

        var sb = new StringBuilder();
        sb.AppendLine($"=== {Region.ToUpper()} ({ElementCount} elements) ===");

        foreach (var e in Elements)
        {
            var indent = new string(' ', e.Depth * 2);
            var states = e.States.Count > 0 ? " " + string.Join(" ", e.States.Select(s => $"[{s}]")) : "";
            sb.AppendLine($"{indent}[{e.Ref}] {e.Role} \"{e.Name}\"{states}");
        }

        return sb.ToString().TrimEnd();
    }

    public object ToStructuredData() => this;
}

public class ReadElement
{
    [JsonPropertyName("ref")]
    public string Ref { get; set; } = "";

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("depth")]
    public int Depth { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("states")]
    public List<string> States { get; set; } = new();
}
