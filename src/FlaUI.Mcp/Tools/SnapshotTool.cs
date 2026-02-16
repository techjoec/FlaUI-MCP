using System.Text.Json;
using FlaUI.Core.AutomationElements;
using PlaywrightWindows.Mcp.Core;
using PlaywrightWindows.Mcp.Models;

namespace PlaywrightWindows.Mcp.Tools;

/// <summary>
/// Take accessibility snapshot of a window - DEPRECATED in favor of scoped tools
/// </summary>
public class SnapshotTool : ToolBase
{
    private readonly SessionManager _sessionManager;
    private readonly ElementRegistry _elementRegistry;
    private readonly SnapshotBuilder _snapshotBuilder;

    public SnapshotTool(SessionManager sessionManager, ElementRegistry elementRegistry)
    {
        _sessionManager = sessionManager;
        _elementRegistry = elementRegistry;
        _snapshotBuilder = new SnapshotBuilder(elementRegistry);
    }

    public override string Name => "windows_snapshot";

    public override string Description =>
        "DEPRECATED: Prefer windows_status, windows_find, or windows_peek for most cases. " +
        "Full accessibility snapshot of a window. Returns 500-5000+ tokens. " +
        "Use only for debugging or when scoped tools are insufficient.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            handle = new
            {
                type = "string",
                description = "Window handle from windows_launch or windows_list_windows. If omitted, uses focused window."
            },
            depth = new
            {
                type = "integer",
                description = "Max tree depth (1-10, default: 5)",
                @default = 5,
                minimum = 1,
                maximum = 10
            },
            filter = new
            {
                type = "string",
                description = "Element filter: 'all', 'interactive' (buttons, inputs), 'text' (labels, text)",
                @enum = new[] { "all", "interactive", "text", "structure" },
                @default = "all"
            },
            max_elements = new
            {
                type = "integer",
                description = "Max elements to return (default: 200, max: 500)",
                @default = 200,
                maximum = 500
            }
        }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var handle = GetStringArgument(arguments, "handle");
        var depth = Math.Min(GetIntArgument(arguments, "depth", 5), 10);
        var filter = GetStringArgument(arguments, "filter") ?? "all";
        var maxElements = Math.Min(GetIntArgument(arguments, "max_elements", 200), 500);
        const int warnThreshold = 100;

        try
        {
            FlaUI.Core.AutomationElements.Window? window = null;

            if (!string.IsNullOrEmpty(handle))
            {
                window = _sessionManager.GetWindow(handle);
                if (window == null)
                {
                    return Task.FromResult(ErrorResult(new McpError
                    {
                        Code = ErrorCodes.WINDOW_NOT_FOUND,
                        Message = $"Window not found: {handle}",
                        Recovery = new List<string>
                        {
                            "Check handle is valid from windows_status or windows_list_windows",
                            "Window may have been closed"
                        }
                    }));
                }
            }
            else
            {
                var focusedElement = _sessionManager.Automation.FocusedElement();
                if (focusedElement != null)
                {
                    var current = focusedElement;
                    while (current != null)
                    {
                        if (current.Properties.ControlType.ValueOrDefault == FlaUI.Core.Definitions.ControlType.Window)
                        {
                            window = current.AsWindow();
                            break;
                        }
                        current = current.Parent;
                    }
                }

                if (window == null)
                {
                    return Task.FromResult(ErrorResult(new McpError
                    {
                        Code = ErrorCodes.WINDOW_NOT_FOUND,
                        Message = "No window found",
                        Recovery = new List<string>
                        {
                            "Ensure a window is active and has focus",
                            "Use windows_list_windows to see available windows"
                        }
                    }));
                }

                handle = _sessionManager.GetOrCreateHandle(window);
            }

            _snapshotBuilder.MaxDepth = depth;
            _snapshotBuilder.Filter = filter;
            _snapshotBuilder.MaxElements = maxElements;

            var snapshot = _snapshotBuilder.BuildSnapshot(handle!, window);

            // Add warnings
            var elementCount = _snapshotBuilder.ElementCount;
            var warnings = new List<string>();

            if (elementCount >= maxElements)
            {
                warnings.Add($"TRUNCATED: {elementCount}+ elements, showing first {maxElements}");
                warnings.Add("Use 'depth' or 'filter' parameters to narrow results");
                warnings.Add("Better: Use windows_find for targeted search");
            }
            else if (elementCount >= warnThreshold)
            {
                warnings.Add($"LARGE TREE: {elementCount} elements");
                warnings.Add("Consider using windows_status, windows_find, or windows_peek");
            }

            if (warnings.Any())
            {
                snapshot += "\n\n[" + string.Join(". ", warnings) + "]";
            }

            return Task.FromResult(TextResult(snapshot));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult(new McpError
            {
                Code = ErrorCodes.ELEMENT_SEARCH_FAILED,
                Message = $"Failed to capture snapshot: {ex.Message}",
                Recovery = new List<string>
                {
                    "Ensure window is visible and not minimized",
                    "Try windows_status first",
                    "Use windows_list_windows to see available handles"
                }
            }));
        }
    }
}
