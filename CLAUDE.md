# FlaUI-MCP — Agent Instructions

MCP server that gives AI agents structured access to Windows desktop UI via accessibility tree snapshots and element refs (like Playwright, but for native Windows apps).

## Quick Start

```bash
# Build on Windows VM and verify everything works
./test/smoke.sh

# Just build
./test/build.sh

# Call any tool ad-hoc
./test/call_tool.sh list
./test/call_tool.sh call windows_status
./test/call_tool.sh call windows_launch '{"app":"notepad.exe"}'
./test/call_tool.sh call windows_snapshot '{"windowId":"w1"}'

# Run stateful integration test (Notepad launch/snapshot/close)
source test/config.sh && $FLAUI_PYTHON test/session_test.py
```

## Editing Workflow

1. Edit source files under `src/FlaUI.Mcp/`
2. `./test/build.sh` — verify it compiles
3. `./test/smoke.sh` — verify MCP protocol works end-to-end
4. `./test/call_tool.sh call <tool>` — test specific tool you changed

## Architecture

```
Linux (this machine)  ──SSH stdio──>  Windows VM (windows-vm)
test/mcp_client.py                    dotnet FlaUI.Mcp.dll
Python MCP SDK                        Hand-rolled MCP server (no SDK)
```

- **Transport:** JSON-RPC over stdio, piped through SSH
- **Protocol:** MCP `2024-11-05`
- **Target framework:** `net10.0-windows` (Windows-only, cannot build on Linux)
- **Namespace:** `PlaywrightWindows.Mcp` (historical, all source uses this)

## Key Files

| File | Purpose |
|------|---------|
| `src/FlaUI.Mcp/Program.cs` | Entry point, registers all 15 tools |
| `src/FlaUI.Mcp/Mcp/McpServer.cs` | JSON-RPC stdio loop |
| `src/FlaUI.Mcp/Mcp/ToolRegistry.cs` | Tool dispatch, `ITool`/`ToolBase` base classes |
| `src/FlaUI.Mcp/Mcp/Protocol.cs` | MCP protocol types |
| `src/FlaUI.Mcp/Core/SessionManager.cs` | Singleton: owns UIA3Automation, window handles, ElementRegistry |
| `src/FlaUI.Mcp/Core/ElementRegistry.cs` | Maps ref IDs (`w1e5`) to live `AutomationElement` objects |
| `src/FlaUI.Mcp/Core/SnapshotBuilder.cs` | Builds accessibility tree text from UIA tree |
| `src/FlaUI.Mcp/Tools/*.cs` | Tool implementations (one class per tool) |

## MCP Tools (15 total)

All prefixed `windows_`:

`windows_launch`, `windows_snapshot`, `windows_click`, `windows_type`, `windows_fill`, `windows_get_text`, `windows_screenshot`, `windows_list_windows`, `windows_focus`, `windows_close`, `windows_batch`, `windows_status`, `windows_find`, `windows_peek`, `windows_read`

## Adding a New Tool

1. Create `src/FlaUI.Mcp/Tools/YourTool.cs` — extend `ToolBase`
2. Set `Name => "windows_your_tool"`, implement `ExecuteAsync`
3. Register in `Program.cs`: `toolRegistry.RegisterTool(new YourTool(...))`
4. Update expected count in `test/smoke.sh` (`EXPECTED_TOOLS`)
5. `./test/build.sh && ./test/call_tool.sh call windows_your_tool '{...}'`

## Conventions

- Tool names: `windows_` prefix, snake_case
- Element refs: `w{windowId}e{elementId}` (e.g., `w1e5`), invalidated each snapshot
- Window handles: `w{N}` (e.g., `w1`, `w2`)
- All tools return `McpToolResult` with `Content` list and `IsError` flag
- Use `TextResult()`, `ErrorResult()`, `SuccessResult()` helpers from `ToolBase`

## Test Configuration

Edit `test/config.sh` to change SSH target, paths, or Python interpreter. Override per-run:

```bash
FLAUI_SSH_HOST=other-vm ./test/smoke.sh
```
