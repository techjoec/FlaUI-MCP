// src/FlaUI.Mcp/Models/McpError.cs
using System.Text;
using System.Text.Json.Serialization;

namespace PlaywrightWindows.Mcp.Models;

/// <summary>
/// Structured error with recovery steps - intentional error disclosure
/// </summary>
public class McpError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("recovery")]
    public List<string> Recovery { get; set; } = new();

    [JsonPropertyName("context")]
    public object? Context { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{Code}] {Message}");

        if (Recovery.Count > 0)
        {
            sb.AppendLine("Recovery steps:");
            foreach (var step in Recovery)
            {
                sb.AppendLine($"  - {step}");
            }
        }

        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// Error code constants for FlaUI-MCP
/// </summary>
public static class ErrorCodes
{
    // Window/Element errors (FW1xxx)
    public const string WINDOW_NOT_FOUND = "FW1001";
    public const string ELEMENT_SEARCH_FAILED = "FW1002";
    public const string ELEMENT_NOT_FOUND = "FW1003";
    public const string ELEMENT_NOT_INTERACTABLE = "FW1004";
    public const string TIMEOUT = "FW1005";

    // Workflow/Template errors (FW2xxx)
    public const string TEMPLATE_NOT_FOUND = "FW2001";
    public const string TEMPLATE_VARIABLE_MISSING = "FW2002";
    public const string WORKFLOW_FAILED = "FW2003";
    public const string WORKFLOW_STEP_FAILED = "FW2004";

    // Session errors (FW3xxx)
    public const string SESSION_NOT_INITIALIZED = "FW3001";
    public const string SESSION_EXPIRED = "FW3002";
}
