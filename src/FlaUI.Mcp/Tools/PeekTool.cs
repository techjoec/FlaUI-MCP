// src/FlaUI.Mcp/Tools/PeekTool.cs
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using PlaywrightWindows.Mcp.Core;
using PlaywrightWindows.Mcp.Models;

namespace PlaywrightWindows.Mcp.Tools;

/// <summary>
/// Peek at focused element + immediate context
/// Ultra-lightweight: ~50 tokens
/// </summary>
public class PeekTool : ToolBase
{
    private readonly SessionManager _sessionManager;

    public PeekTool(SessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    public override string Name => "windows_peek";

    public override string Description =>
        "Ultra-lightweight view of focused element + immediate siblings. ~50 tokens. " +
        "Use to see what's interactable near current focus without tree walking.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            include_siblings = new
            {
                type = "boolean",
                description = "Include sibling elements (default: true)",
                @default = true
            },
            include_parent = new
            {
                type = "boolean",
                description = "Include parent element (default: false)",
                @default = false
            },
            max_siblings = new
            {
                type = "integer",
                description = "Max siblings to show (default: 10, max: 15)",
                @default = 10
            }
        }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var includeSiblings = GetBoolArgument(arguments, "include_siblings", true);
        var includeParent = GetBoolArgument(arguments, "include_parent", false);
        var maxSiblings = Math.Min(GetIntArgument(arguments, "max_siblings", 10), 15);

        try
        {
            var focused = _sessionManager.Automation.FocusedElement();
            var window = FindParentWindow(focused);
            var handle = window != null ? _sessionManager.GetOrCreateHandle(window) : "";

            var children = new List<PeekElement>();

            // Focused element
            children.Add(new PeekElement
            {
                Ref = !string.IsNullOrEmpty(handle) ? _sessionManager.RegisterElement(handle, focused) : "",
                Role = GetRoleName(focused),
                Name = Truncate(focused.Properties.Name.ValueOrDefault, 30) ?? "",
                Enabled = focused.Properties.IsEnabled.ValueOrDefault
            });

            // Siblings
            if (includeSiblings)
            {
                var parent = focused.Parent;
                if (parent != null)
                {
                    var siblingCount = 0;
                    foreach (var sibling in parent.FindAllChildren())
                    {
                        if (sibling == focused) continue;
                        if (siblingCount >= maxSiblings) break;

                        children.Add(new PeekElement
                        {
                            Ref = !string.IsNullOrEmpty(handle) ? _sessionManager.RegisterElement(handle, sibling) : "",
                            Role = GetRoleName(sibling),
                            Name = Truncate(sibling.Properties.Name.ValueOrDefault, 30) ?? "",
                            Enabled = sibling.Properties.IsEnabled.ValueOrDefault
                        });
                        siblingCount++;
                    }
                }
            }

            var result = new PeekResult
            {
                Ref = children.FirstOrDefault()?.Ref ?? "",
                Role = GetRoleName(focused),
                Name = Truncate(focused.Properties.Name.ValueOrDefault, 30),
                ChildCount = focused.Parent?.FindAllChildren().Length ?? 0,
                Children = children
            };

            return Task.FromResult(SuccessResult(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult(new McpError
            {
                Code = ErrorCodes.ELEMENT_NOT_FOUND,
                Message = $"Failed to peek: {ex.Message}",
                Recovery = new List<string>
                {
                    "Ensure an element has focus",
                    "Try windows_status to check current state"
                }
            }));
        }
    }

    private string GetRoleName(AutomationElement element)
    {
        return element.Properties.ControlType.ValueOrDefault switch
        {
            ControlType.Button => "button",
            ControlType.Edit => "textbox",
            ControlType.Text => "text",
            ControlType.CheckBox => "checkbox",
            ControlType.ComboBox => "combobox",
            ControlType.ListItem => "listitem",
            ControlType.MenuItem => "menuitem",
            ControlType.TabItem => "tab",
            _ => "element"
        };
    }

    private Window? FindParentWindow(AutomationElement element)
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
}
