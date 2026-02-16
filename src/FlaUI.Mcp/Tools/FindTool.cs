using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using PlaywrightWindows.Mcp.Core;
using PlaywrightWindows.Mcp.Models;

namespace PlaywrightWindows.Mcp.Tools;

/// <summary>
/// Find elements by criteria - NOT a full tree dump
/// Hard limit of 20 results to prevent token explosion
/// </summary>
public class FindTool : ToolBase
{
    private readonly SessionManager _sessionManager;

    public FindTool(SessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    public override string Name => "windows_find";

    public override string Description =>
        "Find elements matching criteria. Returns refs only, no tree hierarchy. " +
        "~100-200 tokens. Use this when you know what you're looking for. " +
        "Hard limit of 20 results.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            role = new
            {
                type = "string",
                description = "Element role: button, textbox, text, checkbox, radio, combobox, listitem, menuitem, tab, link, image, group, window",
                @enum = new[] { "button", "textbox", "text", "checkbox", "radio", "combobox",
                               "listitem", "menuitem", "tab", "link", "image", "group", "window" }
            },
            name_contains = new
            {
                type = "string",
                description = "Element name contains this text (case-insensitive)"
            },
            name_exact = new
            {
                type = "string",
                description = "Element name matches exactly"
            },
            state = new
            {
                type = "string",
                description = "Filter by state: enabled, disabled, focused, checked, selected",
                @enum = new[] { "enabled", "disabled", "focused", "checked", "selected" }
            },
            handle = new
            {
                type = "string",
                description = "Window handle to search within (uses focused window if omitted)"
            },
            max_results = new
            {
                type = "integer",
                description = "Maximum results (default: 10, max: 20)",
                @default = 10,
                maximum = 20
            }
        },
        required = new[] { "role" }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var role = GetStringArgument(arguments, "role");
        var nameContains = GetStringArgument(arguments, "name_contains");
        var nameExact = GetStringArgument(arguments, "name_exact");
        var state = GetStringArgument(arguments, "state");
        var handle = GetStringArgument(arguments, "handle");
        var maxResults = Math.Min(GetIntArgument(arguments, "max_results", 10), 20);

        if (string.IsNullOrEmpty(role))
        {
            return Task.FromResult(ErrorResult(new McpError
            {
                Code = ErrorCodes.ELEMENT_SEARCH_FAILED,
                Message = "role parameter is required",
                Recovery = new List<string> { "Specify a role: button, textbox, text, checkbox, etc." }
            }));
        }

        try
        {
            // Get search root
            AutomationElement root;
            string searchHandle;

            if (!string.IsNullOrEmpty(handle))
            {
                var window = _sessionManager.GetWindow(handle);
                if (window == null)
                {
                    return Task.FromResult(ErrorResult(new McpError
                    {
                        Code = ErrorCodes.WINDOW_NOT_FOUND,
                        Message = $"Window not found: {handle}",
                        Recovery = new List<string>
                        {
                            "Check handle is valid from windows_status or windows_list_windows",
                            "Window may have been closed",
                            "Use windows_status to get current window handle"
                        }
                    }));
                }
                root = window;
                searchHandle = handle;
            }
            else
            {
                var focusedElement = _sessionManager.Automation.FocusedElement();
                var window = FindParentWindow(focusedElement);
                if (window == null)
                {
                    return Task.FromResult(ErrorResult(new McpError
                    {
                        Code = ErrorCodes.WINDOW_NOT_FOUND,
                        Message = "No window found",
                        Recovery = new List<string>
                        {
                            "Ensure a window is active and has focus"
                        }
                    }));
                }
                root = window;
                searchHandle = _sessionManager.GetOrCreateHandle(window);
            }

            // Search with early termination
            var matches = new List<FindResultElement>();
            var controlType = MapRoleToControlType(role);
            var searched = 0;
            var maxSearch = 1000;

            WalkTree(root, element =>
            {
                searched++;
                if (searched > maxSearch) return false;

                // Role match
                if (element.Properties.ControlType.ValueOrDefault != controlType)
                    return true;

                // Name filter
                var name = element.Properties.Name.ValueOrDefault ?? "";
                if (!string.IsNullOrEmpty(nameExact) && name != nameExact)
                    return true;
                if (!string.IsNullOrEmpty(nameContains) && !MatchesPattern(name, nameContains))
                    return true;

                // State filter
                if (!MatchesState(element, state))
                    return true;

                // Register and add
                var refId = _sessionManager.RegisterElement(searchHandle, element);
                matches.Add(new FindResultElement
                {
                    Ref = refId,
                    Name = Truncate(name, 40) ?? "",
                    Enabled = element.Properties.IsEnabled.ValueOrDefault
                });

                return matches.Count < maxResults;
            });

            var result = new FindResult
            {
                Query = new FindQuery { Role = role, NameContains = nameContains, State = state },
                TotalSearched = Math.Min(searched, maxSearch),
                MatchCount = matches.Count,
                Truncated = searched > maxSearch,
                Elements = matches
            };

            return Task.FromResult(SuccessResult(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult(new McpError
            {
                Code = ErrorCodes.ELEMENT_SEARCH_FAILED,
                Message = $"Failed to search for elements: {ex.Message}",
                Recovery = new List<string>
                {
                    "Ensure a window is active and accessible",
                    "Try simpler search criteria",
                    "Use windows_status first to verify window state"
                }
            }));
        }
    }

    private ControlType MapRoleToControlType(string role) => role switch
    {
        "button" => ControlType.Button,
        "textbox" => ControlType.Edit,
        "text" => ControlType.Text,
        "checkbox" => ControlType.CheckBox,
        "radio" => ControlType.RadioButton,
        "combobox" => ControlType.ComboBox,
        "listitem" => ControlType.ListItem,
        "menuitem" => ControlType.MenuItem,
        "tab" => ControlType.TabItem,
        "link" => ControlType.Hyperlink,
        "image" => ControlType.Image,
        "group" => ControlType.Group,
        "window" => ControlType.Window,
        _ => ControlType.Custom
    };

    private bool MatchesPattern(string name, string pattern)
    {
        var p = pattern.ToLowerInvariant();
        var n = name.ToLowerInvariant();

        if (p.StartsWith("*") && p.EndsWith("*"))
            return n.Contains(p.Trim('*'));
        if (p.StartsWith("*"))
            return n.EndsWith(p[1..]);
        if (p.EndsWith("*"))
            return n.StartsWith(p[..^1]);
        return n.Contains(p);
    }

    private bool MatchesState(AutomationElement element, string? state)
    {
        if (string.IsNullOrEmpty(state)) return true;

        return state switch
        {
            "enabled" => element.Properties.IsEnabled.ValueOrDefault,
            "disabled" => !element.Properties.IsEnabled.ValueOrDefault,
            "focused" => element.Properties.HasKeyboardFocus.ValueOrDefault,
            "checked" => element.Patterns.Toggle.IsSupported &&
                        element.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault == ToggleState.On,
            "selected" => element.Patterns.SelectionItem.IsSupported &&
                         element.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault,
            _ => true
        };
    }

    private void WalkTree(AutomationElement root, Func<AutomationElement, bool> visitor)
    {
        var stack = new Stack<AutomationElement>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visitor(current)) return;

            try
            {
                foreach (var child in current.FindAllChildren())
                {
                    stack.Push(child);
                }
            }
            catch { /* Some elements throw when accessing children */ }
        }
    }

}
