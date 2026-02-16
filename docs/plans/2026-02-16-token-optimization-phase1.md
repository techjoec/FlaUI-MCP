# Token Optimization - Phase 1: Infrastructure Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build the foundational models and helpers that enable token-efficient tool implementations.

**Architecture:** Add structured output models with `IToolResult` interface for dual JSON/text output, extend SessionManager with element tracking helpers, and add utility methods to ToolBase for consistent tool implementation.

**Tech Stack:** C# .NET 8, FlaUI.UIA3, System.Text.Json

---

## Task 1: Create IToolResult Interface and Models Directory

**Files:**
- Create: `src/FlaUI.Mcp/Models/IToolResult.cs`

**Step 1: Create Models directory and IToolResult interface**

```csharp
// src/FlaUI.Mcp/Models/IToolResult.cs
namespace PlaywrightWindows.Mcp.Models;

/// <summary>
/// Base interface for all structured tool results.
/// Provides both compact text (token-efficient) and structured JSON representations.
/// </summary>
public interface IToolResult
{
    /// <summary>
    /// Generate a token-efficient text representation for LLM consumption
    /// </summary>
    string ToCompactString();

    /// <summary>
    /// Return the full structured data object for JSON serialization
    /// </summary>
    object ToStructuredData();
}
```

**Step 2: Verify file compiles**

Run: `cd /github/repos/FlaUI-MCP && dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add src/FlaUI.Mcp/Models/IToolResult.cs
git commit -m "feat: add IToolResult interface for structured tool outputs

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

## Task 2: Create McpError Model with ErrorCodes

**Files:**
- Create: `src/FlaUI.Mcp/Models/McpError.cs`

**Step 1: Write the McpError model**

```csharp
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
```

**Step 2: Verify file compiles**

Run: `cd /github/repos/FlaUI-MCP && dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add src/FlaUI.Mcp/Models/McpError.cs
git commit -m "feat: add McpError model with structured error codes

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

## Task 3: Create StatusResult Model

**Files:**
- Create: `src/FlaUI.Mcp/Models/StatusResult.cs`

**Step 1: Write the StatusResult model**

```csharp
// src/FlaUI.Mcp/Models/StatusResult.cs
using System.Text;
using System.Text.Json.Serialization;

namespace PlaywrightWindows.Mcp.Models;

/// <summary>
/// Result from windows_status tool - lightweight session overview (~80 tokens)
/// </summary>
public class StatusResult : IToolResult
{
    [JsonPropertyName("window")]
    public WindowInfo? Window { get; set; }

    [JsonPropertyName("focused")]
    public FocusedElementInfo Focused { get; set; } = new();

    [JsonPropertyName("session")]
    public SessionInfo Session { get; set; } = new();

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

    public string ToCompactString()
    {
        var sb = new StringBuilder();

        if (Window != null)
        {
            sb.AppendLine($"Window: {Window.Title ?? "none"} [{Window.Process}]");
        }

        sb.AppendLine($"Focused: {Focused.Role} \"{Focused.Name}\" [{Focused.Ref}]");

        if (!string.IsNullOrEmpty(Focused.Value))
            sb.AppendLine($"Value: {Focused.Value}");

        if (!Focused.Enabled)
            sb.AppendLine("State: disabled");

        if (Session.ActiveWindows > 1)
            sb.AppendLine($"Session: {Session.ActiveWindows} windows");

        return sb.ToString().TrimEnd();
    }

    public object ToStructuredData() => this;
}

public class WindowInfo
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("handle")]
    public string? Handle { get; set; }

    [JsonPropertyName("process")]
    public string? Process { get; set; }
}

public class FocusedElementInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("ref")]
    public string? Ref { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("hasKeyboardFocus")]
    public bool HasKeyboardFocus { get; set; }
}

public class SessionInfo
{
    [JsonPropertyName("activeWindows")]
    public int ActiveWindows { get; set; }

    [JsonPropertyName("registeredElements")]
    public int RegisteredElements { get; set; }
}
```

**Step 2: Verify file compiles**

Run: `cd /github/repos/FlaUI-MCP && dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add src/FlaUI.Mcp/Models/StatusResult.cs
git commit -m "feat: add StatusResult model for windows_status tool

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

## Task 4: Create FindResult Model

**Files:**
- Create: `src/FlaUI.Mcp/Models/FindResult.cs`

**Step 1: Write the FindResult model**

```csharp
// src/FlaUI.Mcp/Models/FindResult.cs
using System.Text;
using System.Text.Json.Serialization;

namespace PlaywrightWindows.Mcp.Models;

/// <summary>
/// Result from windows_find tool - scoped element search (~100-200 tokens)
/// </summary>
public class FindResult : IToolResult
{
    [JsonPropertyName("query")]
    public FindQuery Query { get; set; } = new();

    [JsonPropertyName("totalSearched")]
    public int TotalSearched { get; set; }

    [JsonPropertyName("matchCount")]
    public int MatchCount { get; set; }

    [JsonPropertyName("truncated")]
    public bool Truncated { get; set; }

    [JsonPropertyName("elements")]
    public List<FindResultElement> Elements { get; set; } = new();

    public string ToCompactString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Found {MatchCount} {Query.Role}s");

        foreach (var e in Elements)
        {
            var disabled = !e.Enabled ? " [disabled]" : "";
            sb.AppendLine($"  [{e.Ref}] \"{e.Name}\"{disabled}");
        }

        if (Truncated)
            sb.AppendLine($"[Search truncated at {TotalSearched} elements]");

        return sb.ToString().TrimEnd();
    }

    public object ToStructuredData() => this;
}

public class FindQuery
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("nameContains")]
    public string? NameContains { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }
}

public class FindResultElement
{
    [JsonPropertyName("ref")]
    public string Ref { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}
```

**Step 2: Verify file compiles**

Run: `cd /github/repos/FlaUI-MCP && dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add src/FlaUI.Mcp/Models/FindResult.cs
git commit -m "feat: add FindResult model for windows_find tool

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

## Task 5: Create ReadResult Model

**Files:**
- Create: `src/FlaUI.Mcp/Models/ReadResult.cs`

**Step 1: Write the ReadResult model**

```csharp
// src/FlaUI.Mcp/Models/ReadResult.cs
using System.Text;
using System.Text.Json.Serialization;

namespace PlaywrightWindows.Mcp.Models;

/// <summary>
/// Result from windows_read tool - element property inspection (~100 tokens)
/// </summary>
public class ReadResult : IToolResult
{
    [JsonPropertyName("ref")]
    public string Ref { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("visible")]
    public bool Visible { get; set; } = true;

    [JsonPropertyName("focused")]
    public bool Focused { get; set; }

    [JsonPropertyName("bounds")]
    public string? Bounds { get; set; }

    [JsonPropertyName("patterns")]
    public List<string> Patterns { get; set; } = new();

    public string ToCompactString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{Ref}] {Role} \"{Name}\"");

        if (!string.IsNullOrEmpty(Value))
            sb.AppendLine($"Value: {Value}");

        if (!Enabled)
            sb.Append("[disabled] ");
        if (!Visible)
            sb.Append("[hidden] ");
        if (Focused)
            sb.Append("[focused] ");

        if (sb.Length > 0 && sb[^1] == ' ')
            sb.Length--; // Remove trailing space

        return sb.ToString().TrimEnd();
    }

    public object ToStructuredData() => this;
}
```

**Step 2: Verify file compiles**

Run: `cd /github/repos/FlaUI-MCP && dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add src/FlaUI.Mcp/Models/ReadResult.cs
git commit -m "feat: add ReadResult model for windows_read tool

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

## Task 6: Create PeekResult Model

**Files:**
- Create: `src/FlaUI.Mcp/Models/PeekResult.cs`

**Step 1: Write the PeekResult model**

```csharp
// src/FlaUI.Mcp/Models/PeekResult.cs
using System.Text;
using System.Text.Json.Serialization;

namespace PlaywrightWindows.Mcp.Models;

/// <summary>
/// Result from windows_peek tool - lightweight element tree preview (~50 tokens)
/// </summary>
public class PeekResult : IToolResult
{
    [JsonPropertyName("ref")]
    public string Ref { get; set; } = "";

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("childCount")]
    public int ChildCount { get; set; }

    [JsonPropertyName("children")]
    public List<PeekElement> Children { get; set; } = new();

    public string ToCompactString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{Role} \"{Name}\" [{Ref}] - {ChildCount} children");

        foreach (var child in Children)
        {
            var disabled = !child.Enabled ? " [disabled]" : "";
            sb.AppendLine($"  [{child.Ref}] {child.Role} \"{child.Name}\"{disabled}");
        }

        return sb.ToString().TrimEnd();
    }

    public object ToStructuredData() => this;
}

public class PeekElement
{
    [JsonPropertyName("ref")]
    public string Ref { get; set; } = "";

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}
```

**Step 2: Verify file compiles**

Run: `cd /github/repos/FlaUI-MCP && dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add src/FlaUI.Mcp/Models/PeekResult.cs
git commit -m "feat: add PeekResult model for windows_peek tool

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

## Task 7: Create ToolResultExtensions

**Files:**
- Create: `src/FlaUI.Mcp/Models/ToolResultExtensions.cs`

**Step 1: Write the ToolResultExtensions class**

```csharp
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
```

**Step 2: Verify file compiles**

Run: `cd /github/repos/FlaUI-MCP && dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add src/FlaUI.Mcp/Models/ToolResultExtensions.cs
git commit -m "feat: add ToolResultExtensions for McpToolResult creation

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

## Task 8: Extend ToolBase with Helper Methods

**Files:**
- Modify: `src/FlaUI.Mcp/Mcp/ToolRegistry.cs` (add methods to ToolBase class)

**Step 1: Add GetIntArgument method to ToolBase**

Add after `GetBoolArgument` method (line 131):

```csharp
    protected int GetIntArgument(JsonElement? arguments, string name, int defaultValue = 0)
    {
        if (arguments == null) return defaultValue;
        if (!arguments.Value.TryGetProperty(name, out var prop)) return defaultValue;
        if (prop.ValueKind == JsonValueKind.Number)
            return prop.GetInt32();
        return defaultValue;
    }
```

**Step 2: Add GetObjectArgument method to ToolBase**

```csharp
    protected Dictionary<string, object>? GetObjectArgument(JsonElement? arguments, string name)
    {
        if (arguments == null) return null;
        if (!arguments.Value.TryGetProperty(name, out var prop)) return null;
        if (prop.ValueKind == JsonValueKind.Object)
            return JsonSerializer.Deserialize<Dictionary<string, object>>(prop.GetRawText(), McpProtocol.JsonOptions);
        return null;
    }
```

**Step 3: Add Truncate static method to ToolBase**

```csharp
    protected static string? Truncate(string? value, int maxLength)
    {
        if (value == null) return null;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
```

**Step 4: Add typed SuccessResult method to ToolBase**

Add the using statement at top of file (if not present):
```csharp
using PlaywrightWindows.Mcp.Models;
```

Add method after Truncate:
```csharp
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
```

**Step 5: Add ErrorResult (McpError overload) to ToolBase**

```csharp
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
```

**Step 6: Verify file compiles**

Run: `cd /github/repos/FlaUI-MCP && dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj`
Expected: Build succeeds

**Step 7: Commit**

```bash
git add src/FlaUI.Mcp/Mcp/ToolRegistry.cs
git commit -m "feat: extend ToolBase with GetIntArgument, GetObjectArgument, Truncate helpers

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

## Task 9: Extend SessionManager with Tracking Helpers

**Files:**
- Modify: `src/FlaUI.Mcp/Core/SessionManager.cs`

**Step 1: Add ActiveWindowCount property**

Add after `_windowCounter` field (line 17):

```csharp
    /// <summary>
    /// Count of currently tracked windows
    /// </summary>
    public int ActiveWindowCount => _windows.Count;
```

**Step 2: Add TotalElementCount property (requires ElementRegistry reference)**

Add at the top with existing usings, add field for ElementRegistry:

```csharp
    private readonly ElementRegistry _elementRegistry = new();
```

Add property after ActiveWindowCount:

```csharp
    /// <summary>
    /// Total elements registered across all windows
    /// </summary>
    public int TotalElementCount => _elementRegistry.Count;
```

**Step 3: Add GetOrCreateHandle method**

Add after RegisterWindow method (around line 122):

```csharp
    /// <summary>
    /// Get existing handle or create new one for a window
    /// </summary>
    public string GetOrCreateHandle(Window? window)
    {
        if (window == null) return string.Empty;

        // Check if already registered
        var existing = _windows.FirstOrDefault(kvp => kvp.Value.Equals(window));
        if (existing.Value != null)
            return existing.Key;

        return RegisterWindow(window);
    }
```

**Step 4: Add RegisterElement method**

Add after GetOrCreateHandle:

```csharp
    /// <summary>
    /// Register an element and get its ref ID
    /// </summary>
    public string RegisterElement(string windowHandle, AutomationElement element)
    {
        return _elementRegistry.Register(windowHandle, element);
    }
```

**Step 5: Expose ElementRegistry for tools that need direct access**

Add property:

```csharp
    /// <summary>
    /// Access to the element registry for element lookups
    /// </summary>
    public ElementRegistry Elements => _elementRegistry;
```

**Step 6: Add Count property to ElementRegistry**

Modify `src/FlaUI.Mcp/Core/ElementRegistry.cs` - add after HasElement method:

```csharp
    /// <summary>
    /// Total number of registered elements
    /// </summary>
    public int Count => _elements.Count;
```

**Step 7: Verify files compile**

Run: `cd /github/repos/FlaUI-MCP && dotnet build src/FlaUI.Mcp/FlaUI.Mcp.csproj`
Expected: Build succeeds

**Step 8: Commit**

```bash
git add src/FlaUI.Mcp/Core/SessionManager.cs src/FlaUI.Mcp/Core/ElementRegistry.cs
git commit -m "feat: extend SessionManager with ActiveWindowCount, GetOrCreateHandle, RegisterElement

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

## Task 10: Final Verification and Build

**Step 1: Run full build**

Run: `cd /github/repos/FlaUI-MCP && dotnet build`
Expected: Build succeeds with no warnings

**Step 2: Verify all Models are present**

Run: `ls -la src/FlaUI.Mcp/Models/`
Expected: 6 files present:
- IToolResult.cs
- McpError.cs
- StatusResult.cs
- FindResult.cs
- ReadResult.cs
- PeekResult.cs
- ToolResultExtensions.cs

**Step 3: Review git log**

Run: `git log --oneline -10`
Expected: 9 commits from this session

---

## Summary

This plan implements **Phase 1: Infrastructure** from Issue #15:

| Issue | Description | Status |
|-------|-------------|--------|
| #12 | SessionManager Extensions | Completed |
| #13 | ToolBase Helper Extensions | Completed |
| #8 | Structured Output Models | Completed |

**Next Session:** Phase 2 - Core Tools (Issues #2, #3, #5, #6, #7)
