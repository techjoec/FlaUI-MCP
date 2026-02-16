// src/FlaUI.Mcp/Models/ToolResultExtensions.cs
using System.Text.Json;

namespace PlaywrightWindows.Mcp.Models;

/// <summary>
/// Extension methods for creating McpToolResult from typed result models
/// </summary>
public static class ToolResultExtensions
{
    /// <summary>
    /// Create a success McpToolResult from a typed result model
    /// </summary>
    public static McpToolResult ToMcpResult(this IToolResult result)
    {
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result.ToStructuredData(), McpProtocol.JsonOptions)
                }
            },
            IsError = false
        };
    }

    /// <summary>
    /// Create an error McpToolResult from structured error
    /// </summary>
    public static McpToolResult ToMcpResult(this McpError error)
    {
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(new { error }, McpProtocol.JsonOptions)
                }
            },
            IsError = true
        };
    }

    /// <summary>
    /// Create a simple text result
    /// </summary>
    public static McpToolResult ToTextResult(string text)
    {
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            },
            IsError = false
        };
    }
}
