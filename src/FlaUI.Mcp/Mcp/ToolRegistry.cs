using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using PlaywrightWindows.Mcp.Core;
using PlaywrightWindows.Mcp.Models;

namespace PlaywrightWindows.Mcp;

/// <summary>
/// Registry for MCP tools - maps tool names to handlers
/// </summary>
public class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new();

    public void RegisterTool(ITool tool)
    {
        _tools[tool.Name] = tool;
    }

    public List<McpTool> GetToolDefinitions()
    {
        return _tools.Values.Select(t => t.GetDefinition()).ToList();
    }

    public async Task<McpToolResult> ExecuteToolAsync(string name, JsonElement? arguments)
    {
        if (!_tools.TryGetValue(name, out var tool))
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new() { Type = "text", Text = $"Unknown tool: {name}" }
                },
                IsError = true
            };
        }

        try
        {
            return await tool.ExecuteAsync(arguments);
        }
        catch (Exception ex)
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new() { Type = "text", Text = $"Error: {ex.Message}" }
                },
                IsError = true
            };
        }
    }
}

/// <summary>
/// Interface for MCP tools
/// </summary>
public interface ITool
{
    string Name { get; }
    McpTool GetDefinition();
    Task<McpToolResult> ExecuteAsync(JsonElement? arguments);
}

/// <summary>
/// Base class for tools with common utilities
/// </summary>
public abstract class ToolBase : ITool
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract object InputSchema { get; }

    public McpTool GetDefinition() => new()
    {
        Name = Name,
        Description = Description,
        InputSchema = InputSchema
    };

    public abstract Task<McpToolResult> ExecuteAsync(JsonElement? arguments);

    protected static McpToolResult TextResult(string text) => new()
    {
        Content = new List<McpContent>
        {
            new() { Type = "text", Text = text }
        }
    };

    protected static McpToolResult ErrorResult(string message) => new()
    {
        Content = new List<McpContent>
        {
            new() { Type = "text", Text = message }
        },
        IsError = true
    };

    protected static McpToolResult ImageResult(byte[] imageData, string mimeType = "image/png") => new()
    {
        Content = new List<McpContent>
        {
            new() 
            { 
                Type = "image", 
                Data = Convert.ToBase64String(imageData),
                MimeType = mimeType
            }
        }
    };

    protected T? GetArgument<T>(JsonElement? arguments, string name)
    {
        if (arguments == null) return default;
        if (!arguments.Value.TryGetProperty(name, out var prop)) return default;
        return JsonSerializer.Deserialize<T>(prop.GetRawText(), McpProtocol.JsonOptions);
    }

    protected string? GetStringArgument(JsonElement? arguments, string name)
    {
        if (arguments == null) return null;
        if (!arguments.Value.TryGetProperty(name, out var prop)) return null;
        return prop.GetString();
    }

    protected bool GetBoolArgument(JsonElement? arguments, string name, bool defaultValue = false)
    {
        if (arguments == null) return defaultValue;
        if (!arguments.Value.TryGetProperty(name, out var prop)) return defaultValue;
        return prop.GetBoolean();
    }

    protected int GetIntArgument(JsonElement? arguments, string name, int defaultValue = 0)
    {
        if (arguments == null) return defaultValue;
        if (!arguments.Value.TryGetProperty(name, out var prop)) return defaultValue;
        if (prop.ValueKind == JsonValueKind.Number)
            return prop.GetInt32();
        return defaultValue;
    }

    protected Dictionary<string, object>? GetObjectArgument(JsonElement? arguments, string name)
    {
        if (arguments == null) return null;
        if (!arguments.Value.TryGetProperty(name, out var prop)) return null;
        if (prop.ValueKind == JsonValueKind.Object)
            return JsonSerializer.Deserialize<Dictionary<string, object>>(prop.GetRawText(), McpProtocol.JsonOptions);
        return null;
    }

    protected static string? Truncate(string? value, int maxLength)
    {
        if (value == null) return null;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }

    protected static McpToolResult SuccessResult(IToolResult result)
    {
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = JsonSerializer.Serialize(result.ToStructuredData(), McpProtocol.JsonOptions) }
            },
            IsError = false
        };
    }

    protected static McpToolResult ErrorResult(McpError error)
    {
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = JsonSerializer.Serialize(new { error }, McpProtocol.JsonOptions) }
            },
            IsError = true
        };
    }

    protected static Window? FindParentWindow(AutomationElement element)
    {
        var current = element;
        while (current != null)
        {
            if (current.Properties.ControlType.ValueOrDefault == ControlType.Window)
                return current.AsWindow();
            current = current.Parent;
        }
        return null;
    }

    protected static string GetRoleName(AutomationElement element)
        => AutomationHelpers.GetRoleName(element);

    protected static string? GetElementName(AutomationElement element)
        => AutomationHelpers.GetElementName(element);

    protected static List<string> GetStates(AutomationElement element)
        => AutomationHelpers.GetStates(element);
}
