// src/FlaUI.Mcp/Models/StatusResult.cs
using System.Text;
using System.Text.Json.Serialization;

namespace PlaywrightWindows.Mcp.Models;

/// <summary>
/// Result from windows_status tool - lightweight session overview (~80 tokens)
/// </summary>
public class StatusResult : IToolResult
{
    [JsonPropertyName("window")]
    public WindowInfo? Window { get; set; }

    [JsonPropertyName("focused")]
    public FocusedElementInfo Focused { get; set; } = new();

    [JsonPropertyName("session")]
    public SessionInfo Session { get; set; } = new();

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

    public string ToCompactString()
    {
        var sb = new StringBuilder();

        if (Window != null)
        {
            sb.AppendLine($"Window: {Window.Title ?? "none"} [{Window.Process}]");
        }

        sb.AppendLine($"Focused: {Focused.Role} \"{Focused.Name}\" [{Focused.Ref}]");

        if (!string.IsNullOrEmpty(Focused.Value))
            sb.AppendLine($"Value: {Focused.Value}");

        if (!Focused.Enabled)
            sb.AppendLine("State: disabled");

        if (Session.ActiveWindows > 1)
            sb.AppendLine($"Session: {Session.ActiveWindows} windows");

        return sb.ToString().TrimEnd();
    }

    public object ToStructuredData() => this;
}

public class WindowInfo
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("handle")]
    public string? Handle { get; set; }

    [JsonPropertyName("process")]
    public string? Process { get; set; }
}

public class FocusedElementInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("ref")]
    public string? Ref { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("hasKeyboardFocus")]
    public bool HasKeyboardFocus { get; set; }
}

public class SessionInfo
{
    [JsonPropertyName("activeWindows")]
    public int ActiveWindows { get; set; }

    [JsonPropertyName("registeredElements")]
    public int RegisteredElements { get; set; }
}
