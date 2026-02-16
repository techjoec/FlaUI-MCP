// src/FlaUI.Mcp/Tools/StatusTool.cs
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using PlaywrightWindows.Mcp.Core;
using PlaywrightWindows.Mcp.Models;

namespace PlaywrightWindows.Mcp.Tools;

/// <summary>
/// Quick status check - THE FIRST TOOL TO CALL
/// Returns window title, focused element, process info in ~80 tokens
/// </summary>
public class StatusTool : ToolBase
{
    private readonly SessionManager _sessionManager;

    public StatusTool(SessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    public override string Name => "windows_status";

    public override string Description =>
        "Quick session overview: active window, focused element, process name. " +
        "~80 tokens. CALL THIS FIRST before any other tool to understand current state. " +
        "Returns refs for immediate interaction.";

    public override object InputSchema => new { type = "object", properties = new { } };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        try
        {
            var automation = _sessionManager.Automation;
            var focusedElement = automation.FocusedElement();

            // Find the window containing focused element
            var window = FindParentWindow(focusedElement);
            string? windowHandle = null;

            if (window != null)
            {
                windowHandle = _sessionManager.GetOrCreateHandle(window);
            }

            // Build minimal response
            var result = new StatusResult
            {
                Window = window != null ? new WindowInfo
                {
                    Title = Truncate(window.Title, 50),
                    Handle = windowHandle,
                    Process = window.Properties.ProcessId.TryGetValue(out var pid) && pid > 0
                        ? GetProcessName(pid)
                        : "unknown"
                } : null,
                Focused = new FocusedElementInfo
                {
                    Name = Truncate(focusedElement.Properties.Name.ValueOrDefault, 30),
                    Role = GetRoleName(focusedElement),
                    Ref = windowHandle != null
                        ? _sessionManager.RegisterElement(windowHandle, focusedElement)
                        : null,
                    Value = Truncate(GetValuePattern(focusedElement), 20),
                    Enabled = focusedElement.Properties.IsEnabled.ValueOrDefault,
                    HasKeyboardFocus = focusedElement.Properties.HasKeyboardFocus.ValueOrDefault
                },
                Session = new SessionInfo
                {
                    ActiveWindows = _sessionManager.ActiveWindowCount,
                    RegisteredElements = _sessionManager.TotalElementCount
                },
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            return Task.FromResult(SuccessResult(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult(new McpError
            {
                Code = ErrorCodes.WINDOW_NOT_FOUND,
                Message = $"Failed to get window status: {ex.Message}",
                Recovery = new List<string>
                {
                    "Ensure a Windows application has focus",
                    "Check if the target window is minimized",
                    "Try windows_list_windows to see available windows"
                }
            }));
        }
    }

    private string? GetProcessName(int pid)
    {
        try { return System.Diagnostics.Process.GetProcessById(pid)?.ProcessName; }
        catch { return null; }
    }

    private string? GetValuePattern(AutomationElement element)
    {
        try
        {
            if (element.Patterns.Value.IsSupported)
                return element.Patterns.Value.Pattern.Value.ValueOrDefault;
        }
        catch { }
        return null;
    }
}
