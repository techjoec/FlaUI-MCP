// src/FlaUI.Mcp/Tools/ReadTool.cs
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using PlaywrightWindows.Mcp.Core;
using PlaywrightWindows.Mcp.Models;

namespace PlaywrightWindows.Mcp.Tools;

/// <summary>
/// Read a specific UI region without dumping entire tree.
/// ~100-200 tokens per region. Max 50 elements.
/// </summary>
public class ReadTool : ToolBase
{
    private readonly SessionManager _sessionManager;
    private const int MaxElements = 50;

    public ReadTool(SessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    public override string Name => "windows_read";

    public override string Description =>
        "Read a specific UI region without dumping entire tree. ~100-200 tokens. " +
        "Regions: 'focused' (current element + children), 'menu' (menu bar items), " +
        "'status' (status bar text), 'dialog' (modal content), 'titlebar' (window controls), " +
        "'toolbar' (toolbar buttons).";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            region = new
            {
                type = "string",
                @enum = new[] { "focused", "menu", "status", "dialog", "titlebar", "toolbar" },
                description = "Which UI region to read"
            },
            handle = new
            {
                type = "string",
                description = "Window handle (uses focused window if omitted)"
            },
            depth = new
            {
                type = "integer",
                description = "Tree depth within region (default: 2, max: 5)",
                @default = 2
            }
        },
        required = new[] { "region" }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var region = GetStringArgument(arguments, "region");
        var handle = GetStringArgument(arguments, "handle");
        var depth = Math.Min(GetIntArgument(arguments, "depth", 2), 5);

        if (string.IsNullOrEmpty(region))
        {
            return Task.FromResult(ErrorResult(new McpError
            {
                Code = ErrorCodes.ELEMENT_SEARCH_FAILED,
                Message = "region parameter is required",
                Recovery = new List<string> { "Specify a region: focused, menu, status, dialog, titlebar, toolbar" }
            }));
        }

        try
        {
            // Resolve window
            Window? window = null;
            string windowHandle;

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
                            "Check handle from windows_status or windows_list_windows",
                            "Window may have been closed"
                        }
                    }));
                }
                windowHandle = handle;
            }
            else
            {
                var focused = _sessionManager.Automation.FocusedElement();
                window = FindParentWindow(focused);
                if (window == null)
                {
                    return Task.FromResult(ErrorResult(new McpError
                    {
                        Code = ErrorCodes.WINDOW_NOT_FOUND,
                        Message = "No active window found",
                        Recovery = new List<string>
                        {
                            "Ensure a window is active and has focus",
                            "Try windows_list_windows to see available windows"
                        }
                    }));
                }
                windowHandle = _sessionManager.GetOrCreateHandle(window);
            }

            // Find region root element
            var regionRoot = FindRegionRoot(window, region);

            if (regionRoot == null)
            {
                return Task.FromResult(SuccessResult(new ReadResult
                {
                    Region = region,
                    Found = false,
                    Message = $"No {region} region found in this window"
                }));
            }

            // Walk the region subtree (without clearing element registry)
            var elements = new List<ReadElement>();
            WalkRegion(regionRoot, windowHandle, elements, 0, depth);

            return Task.FromResult(SuccessResult(new ReadResult
            {
                Region = region,
                Found = true,
                ElementCount = elements.Count,
                Elements = elements
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult(new McpError
            {
                Code = ErrorCodes.ELEMENT_SEARCH_FAILED,
                Message = $"Failed to read region '{region}': {ex.Message}",
                Recovery = new List<string>
                {
                    "Ensure a window is active",
                    "Try windows_status first to verify window state",
                    "The region may not exist in this window type"
                }
            }));
        }
    }

    private AutomationElement? FindRegionRoot(Window window, string region)
    {
        try
        {
            return region switch
            {
                "focused" => _sessionManager.Automation.FocusedElement(),
                "menu" => window.FindFirstDescendant(cf =>
                    cf.ByControlType(ControlType.MenuBar)),
                "status" => window.FindFirstDescendant(cf =>
                    cf.ByControlType(ControlType.StatusBar)),
                "dialog" => FindDialog(window),
                "titlebar" => window.FindFirstDescendant(cf =>
                    cf.ByControlType(ControlType.TitleBar)),
                "toolbar" => window.FindFirstDescendant(cf =>
                    cf.ByControlType(ControlType.ToolBar)),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private AutomationElement? FindDialog(Window window)
    {
        // Look for child windows (modal dialogs are child windows)
        var childWindow = window.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Window));
        if (childWindow != null) return childWindow;

        // Fallback: look for a pane that looks like a dialog (has buttons + text)
        var panes = window.FindAllDescendants(cf =>
            cf.ByControlType(ControlType.Pane));
        foreach (var pane in panes)
        {
            try
            {
                var hasButton = pane.FindFirstDescendant(cf =>
                    cf.ByControlType(ControlType.Button)) != null;
                var hasText = pane.FindFirstDescendant(cf =>
                    cf.ByControlType(ControlType.Text)) != null;
                if (hasButton && hasText) return pane;
            }
            catch { }
        }

        return null;
    }

    private void WalkRegion(AutomationElement root, string windowHandle,
        List<ReadElement> elements, int currentDepth, int maxDepth)
    {
        if (elements.Count >= MaxElements) return;
        if (currentDepth > maxDepth) return;

        // Register and add this element
        var name = GetElementName(root);
        var role = GetRoleName(root);

        // Skip noise elements without names (except structural ones)
        if (!string.IsNullOrEmpty(name) || IsSignificantRole(role))
        {
            var refId = _sessionManager.RegisterElement(windowHandle, root);
            var states = GetStates(root);

            elements.Add(new ReadElement
            {
                Ref = refId,
                Role = role,
                Name = Truncate(name, 40),
                Depth = currentDepth,
                Enabled = root.Properties.IsEnabled.ValueOrDefault,
                States = states
            });
        }

        // Walk children
        try
        {
            foreach (var child in root.FindAllChildren())
            {
                if (elements.Count >= MaxElements) return;
                WalkRegion(child, windowHandle, elements, currentDepth + 1, maxDepth);
            }
        }
        catch { /* Some elements throw when accessing children */ }
    }

    private bool IsSignificantRole(string role)
    {
        return role is "button" or "textbox" or "checkbox" or "radio" or "combobox"
            or "listitem" or "menuitem" or "tab" or "link" or "menu" or "menubar"
            or "toolbar" or "list" or "group" or "window";
    }

}
