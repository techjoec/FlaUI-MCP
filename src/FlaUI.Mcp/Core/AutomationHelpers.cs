using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace PlaywrightWindows.Mcp.Core;

/// <summary>
/// Shared helpers for mapping UIA elements to agent-friendly names, roles, and states.
/// Used by ToolBase, SnapshotBuilder, and any code that needs consistent role/name output.
/// </summary>
public static class AutomationHelpers
{
    public static string GetRoleName(AutomationElement element)
    {
        try
        {
            return element.Properties.ControlType.ValueOrDefault switch
            {
                ControlType.Button => "button",
                ControlType.Edit => "textbox",
                ControlType.Text => "text",
                ControlType.CheckBox => "checkbox",
                ControlType.RadioButton => "radio",
                ControlType.ComboBox => "combobox",
                ControlType.List => "list",
                ControlType.ListItem => "listitem",
                ControlType.Menu => "menu",
                ControlType.MenuItem => "menuitem",
                ControlType.MenuBar => "menubar",
                ControlType.Tree => "tree",
                ControlType.TreeItem => "treeitem",
                ControlType.Tab => "tablist",
                ControlType.TabItem => "tab",
                ControlType.Table => "table",
                ControlType.DataItem => "row",
                ControlType.Header => "header",
                ControlType.HeaderItem => "columnheader",
                ControlType.Slider => "slider",
                ControlType.Spinner => "spinbutton",
                ControlType.ProgressBar => "progressbar",
                ControlType.Hyperlink => "link",
                ControlType.Image => "image",
                ControlType.Pane => "group",
                ControlType.Group => "group",
                ControlType.Window => "window",
                ControlType.Document => "document",
                ControlType.ToolBar => "toolbar",
                ControlType.ToolTip => "tooltip",
                ControlType.ScrollBar => "scrollbar",
                ControlType.StatusBar => "status",
                ControlType.Separator => "separator",
                ControlType.Thumb => "thumb",
                ControlType.TitleBar => "titlebar",
                ControlType.DataGrid => "grid",
                ControlType.Custom => "custom",
                _ => "element"
            };
        }
        catch { return "element"; }
    }

    public static string? GetElementName(AutomationElement element)
    {
        try
        {
            var name = element.Properties.Name.ValueOrDefault;
            if (!string.IsNullOrWhiteSpace(name)) return name;

            var automationId = element.Properties.AutomationId.ValueOrDefault;
            if (!string.IsNullOrWhiteSpace(automationId) && automationId.Length < 50)
                return $"[{automationId}]";
        }
        catch { }
        return null;
    }

    public static List<string> GetStates(AutomationElement element)
    {
        var states = new List<string>();
        try
        {
            if (!element.Properties.IsEnabled.ValueOrDefault)
                states.Add("disabled");

            if (element.Properties.IsOffscreen.ValueOrDefault)
                states.Add("offscreen");

            if (element.Patterns.Value.IsSupported)
            {
                var valuePattern = element.Patterns.Value.Pattern;
                if (valuePattern.IsReadOnly.ValueOrDefault)
                    states.Add("readonly");
            }

            if (element.Patterns.Toggle.IsSupported)
            {
                var toggleState = element.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault;
                if (toggleState == ToggleState.On)
                    states.Add("checked");
                else if (toggleState == ToggleState.Indeterminate)
                    states.Add("indeterminate");
            }

            if (element.Patterns.SelectionItem.IsSupported)
            {
                if (element.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault)
                    states.Add("selected");
            }

            if (element.Patterns.ExpandCollapse.IsSupported)
            {
                var expandState = element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.ValueOrDefault;
                if (expandState == ExpandCollapseState.Expanded)
                    states.Add("expanded");
                else if (expandState == ExpandCollapseState.Collapsed)
                    states.Add("collapsed");
            }
        }
        catch { }
        return states;
    }
}
