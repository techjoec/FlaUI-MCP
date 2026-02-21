using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace PlaywrightWindows.Mcp.Core;

/// <summary>
/// Builds agent-friendly accessibility snapshots from UI Automation trees
/// </summary>
public class SnapshotBuilder
{
    private readonly ElementRegistry _elementRegistry;

    public int MaxDepth { get; set; } = 10;
    public string Filter { get; set; } = "all";
    public int MaxElements { get; set; } = 200;
    public int MaxNameLength { get; set; } = 50;
    public int ElementCount { get; private set; }
    public bool WasTruncated { get; private set; }

    public SnapshotBuilder(ElementRegistry elementRegistry)
    {
        _elementRegistry = elementRegistry;
    }

    public string BuildSnapshot(string windowHandle, AutomationElement root)
    {
        // Reset state
        ElementCount = 0;
        WasTruncated = false;
        _elementRegistry.ClearWindow(windowHandle);

        var sb = new StringBuilder();
        BuildElementSnapshot(sb, windowHandle, root, 0);
        return sb.ToString();
    }

    private void BuildElementSnapshot(StringBuilder sb, string windowHandle, AutomationElement element, int depth)
    {
        // Check limits
        if (ElementCount >= MaxElements)
        {
            WasTruncated = true;
            return;
        }

        if (depth > MaxDepth) return;

        // Skip elements with no meaningful content
        var name = AutomationHelpers.GetElementName(element);
        var role = AutomationHelpers.GetRoleName(element);

        // Apply filter
        if (!PassesFilter(role))
        {
            // Still process children even if parent is filtered
            try
            {
                foreach (var child in element.FindAllChildren())
                {
                    BuildElementSnapshot(sb, windowHandle, child, depth);
                }
            }
            catch { }
            return;
        }

        // Skip some noise elements, but keep elements with names or important roles
        if (ShouldSkipElement(element, name, role))
        {
            try
            {
                foreach (var child in element.FindAllChildren())
                {
                    BuildElementSnapshot(sb, windowHandle, child, depth + 1);
                }
            }
            catch { }
            return;
        }

        // Register element and get ref
        var refId = _elementRegistry.Register(windowHandle, element);
        ElementCount++;

        // Truncate name if needed
        var truncatedName = TruncateName(name, MaxNameLength);

        // Build the line
        var indent = new string(' ', depth * 2);
        var line = BuildElementLine(element, refId, truncatedName, role);
        sb.AppendLine($"{indent}- {line}");

        // Process children
        try
        {
            var children = element.FindAllChildren();
            foreach (var child in children)
            {
                BuildElementSnapshot(sb, windowHandle, child, depth + 1);
            }
        }
        catch
        {
            // Some elements throw when accessing children
        }
    }

    private bool PassesFilter(string role)
    {
        return Filter switch
        {
            "interactive" => role is "button" or "textbox" or "checkbox" or "radio"
                or "combobox" or "listitem" or "menuitem" or "tab" or "link" or "slider",
            "text" => role is "text" or "textbox" or "document",
            "structure" => role is "window" or "group" or "list" or "tree"
                or "tablist" or "menu" or "toolbar" or "grid" or "table",
            _ => true  // "all"
        };
    }

    private string? TruncateName(string? name, int maxLength)
    {
        if (name == null || name.Length <= maxLength) return name;
        return name[..(maxLength - 3)] + "...";
    }

    private string BuildElementLine(AutomationElement element, string refId, string? name, string role)
    {
        var parts = new List<string>();

        // Role first
        parts.Add(role);

        // Name in quotes if present
        if (!string.IsNullOrEmpty(name))
        {
            parts.Add($"\"{EscapeName(name)}\"");
        }

        // Ref
        parts.Add($"[ref={refId}]");

        // State indicators
        var states = AutomationHelpers.GetStates(element);
        if (states.Count > 0)
        {
            parts.AddRange(states.Select(s => $"[{s}]"));
        }

        return string.Join(" ", parts);
    }

    private bool ShouldSkipElement(AutomationElement element, string? name, string role)
    {
        // Always include named elements
        if (!string.IsNullOrEmpty(name)) return false;

        // Always include actionable element types
        if (role is "button" or "textbox" or "checkbox" or "radio" or "combobox"
            or "listitem" or "menuitem" or "tab" or "treeitem" or "link" or "slider")
        {
            return false;
        }

        // Include structural elements that might contain others
        if (role is "window" or "group" or "list" or "tree" or "tablist"
            or "menu" or "menubar" or "toolbar" or "grid" or "table")
        {
            return false;
        }

        // Skip decorative/structural elements without names
        if (role is "element" or "thumb" or "scrollbar" or "separator" or "titlebar")
        {
            return true;
        }

        return false;
    }

    private string EscapeName(string name)
    {
        return name
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "");
    }
}
