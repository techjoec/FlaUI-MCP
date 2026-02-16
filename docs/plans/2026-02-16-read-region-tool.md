# Read Region Tool (windows_read) Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add `windows_read` tool for reading specific UI regions (menu, status bar, dialog, etc.) without dumping the full tree. ~100-200 tokens per region.

**Architecture:** Repurpose existing `ReadResult` model for region output. New `ReadTool` walks a region subtree manually (not via `SnapshotBuilder`, which calls `ClearWindow()`). Follows established tool patterns: constructor takes only `SessionManager`, uses `_sessionManager.RegisterElement()` for element refs, uses `ToolBase` helpers.

**Tech Stack:** C# .NET 8, FlaUI.UIA3, System.Text.Json

---

## Task 1: Repurpose ReadResult Model

**Files:**
- Modify: `src/FlaUI.Mcp/Models/ReadResult.cs`

**Step 1: Rewrite ReadResult for region output**

Replace the entire file with:

```csharp
// src/FlaUI.Mcp/Models/ReadResult.cs
using System.Text;
using System.Text.Json.Serialization;

namespace PlaywrightWindows.Mcp.Models;

/// <summary>
/// Result from windows_read tool - region-scoped UI snapshot (~100-200 tokens)
/// </summary>
public class ReadResult : IToolResult
{
    [JsonPropertyName("region")]
    public string Region { get; set; } = "";

    [JsonPropertyName("found")]
    public bool Found { get; set; }

    [JsonPropertyName("elementCount")]
    public int ElementCount { get; set; }

    [JsonPropertyName("elements")]
    public List<ReadElement> Elements { get; set; } = new();

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    public string ToCompactString()
    {
        if (!Found)
            return $"Region '{Region}': not found. {Message}";

        var sb = new StringBuilder();
        sb.AppendLine($"=== {Region.ToUpper()} ({ElementCount} elements) ===");

        foreach (var e in Elements)
        {
            var indent = new string(' ', e.Depth * 2);
            var states = e.States.Count > 0 ? " " + string.Join(" ", e.States.Select(s => $"[{s}]")) : "";
            sb.AppendLine($"{indent}[{e.Ref}] {e.Role} \"{e.Name}\"{states}");
        }

        return sb.ToString().TrimEnd();
    }

    public object ToStructuredData() => this;
}

public class ReadElement
{
    [JsonPropertyName("ref")]
    public string Ref { get; set; } = "";

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("depth")]
    public int Depth { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("states")]
    public List<string> States { get; set; } = new();
}
```

**Step 2: Verify it compiles**

Run: `cd /github/repos/FlaUI-MCP && dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -p:EnableWindowsTargeting=true`
Expected: Build succeeds (no other files reference the old ReadResult shape)

**Step 3: Commit**

```bash
git add src/FlaUI.Mcp/Models/ReadResult.cs
git commit -m "refactor: repurpose ReadResult model for region-based output

Old shape was speculative element-inspection model. New shape matches
what windows_read actually needs: region name, found flag, element list
with depth for tree rendering.

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

## Task 2: Create ReadTool

**Files:**
- Create: `src/FlaUI.Mcp/Tools/ReadTool.cs`

**Step 1: Write ReadTool**

```csharp
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

    private string? GetElementName(AutomationElement element)
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

    private string GetRoleName(AutomationElement element)
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
                ControlType.ListItem => "listitem",
                ControlType.MenuItem => "menuitem",
                ControlType.Menu => "menu",
                ControlType.MenuBar => "menubar",
                ControlType.TabItem => "tab",
                ControlType.ToolBar => "toolbar",
                ControlType.StatusBar => "status",
                ControlType.TitleBar => "titlebar",
                ControlType.List => "list",
                ControlType.Group => "group",
                ControlType.Window => "window",
                ControlType.Hyperlink => "link",
                ControlType.Image => "image",
                ControlType.Pane => "group",
                _ => "element"
            };
        }
        catch { return "element"; }
    }

    private List<string> GetStates(AutomationElement element)
    {
        var states = new List<string>();
        try
        {
            if (!element.Properties.IsEnabled.ValueOrDefault)
                states.Add("disabled");

            if (element.Patterns.Toggle.IsSupported)
            {
                var toggleState = element.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault;
                if (toggleState == ToggleState.On) states.Add("checked");
            }

            if (element.Patterns.SelectionItem.IsSupported)
            {
                if (element.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault)
                    states.Add("selected");
            }

            if (element.Patterns.ExpandCollapse.IsSupported)
            {
                var expandState = element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.ValueOrDefault;
                if (expandState == ExpandCollapseState.Expanded) states.Add("expanded");
                else if (expandState == ExpandCollapseState.Collapsed) states.Add("collapsed");
            }
        }
        catch { }
        return states;
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
```

**Step 2: Verify it compiles**

Run: `cd /github/repos/FlaUI-MCP && dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -p:EnableWindowsTargeting=true`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add src/FlaUI.Mcp/Tools/ReadTool.cs
git commit -m "feat: add ReadTool for region-scoped UI reading

Reads specific UI regions (menu, status, dialog, titlebar, toolbar,
focused) without dumping the full tree. Walks subtree manually to
avoid SnapshotBuilder's ClearWindow() side effect. Max 50 elements
per region, ~100-200 tokens.

Closes #5

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

## Task 3: Register ReadTool in Program.cs

**Files:**
- Modify: `src/FlaUI.Mcp/Program.cs:24`

**Step 1: Add ReadTool registration**

After line 24 (`toolRegistry.RegisterTool(new PeekTool(sessionManager));`), add:

```csharp
toolRegistry.RegisterTool(new ReadTool(sessionManager));
```

**Step 2: Verify it compiles**

Run: `cd /github/repos/FlaUI-MCP && dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -p:EnableWindowsTargeting=true`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add src/FlaUI.Mcp/Program.cs
git commit -m "feat: register ReadTool in MCP server

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

## Task 4: Verify Full Build and Review

**Step 1: Clean build**

Run: `cd /github/repos/FlaUI-MCP && dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -p:EnableWindowsTargeting=true --no-incremental`
Expected: Build succeeds with 0 errors

**Step 2: Check for warnings**

Run: `cd /github/repos/FlaUI-MCP && dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj -p:EnableWindowsTargeting=true 2>&1 | grep -i warning`
Expected: Only pre-existing CS0414 warning for `_appCounter` (#16)

**Step 3: Review git log**

Run: `git log --oneline -5`
Expected: 3 new commits (ReadResult refactor, ReadTool, registration)

---

## Summary

| What | Detail |
|------|--------|
| Issue | #5 Read Region Tool |
| New file | `src/FlaUI.Mcp/Tools/ReadTool.cs` |
| Modified | `src/FlaUI.Mcp/Models/ReadResult.cs`, `src/FlaUI.Mcp/Program.cs` |
| Regions | focused, menu, status, dialog, titlebar, toolbar |
| Token budget | ~100-200 per region, max 50 elements |
| Key design choice | Walks tree manually, does NOT use SnapshotBuilder (avoids ClearWindow) |
| Key design choice | ReadResult repurposed from unused element-inspection shape to region output |
