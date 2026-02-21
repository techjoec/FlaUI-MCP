#!/usr/bin/env python3
"""MCP client for FlaUI-MCP testing.

Connects to the MCP server via SSH and provides three modes:
  list     - List all available tools
  call     - Call a single tool and print the response
  session  - Interactive session reading commands from stdin

Also importable as a library for other test scripts.

Usage:
  python mcp_client.py list
  python mcp_client.py call windows_status
  python mcp_client.py call windows_launch '{"app":"notepad.exe"}'
  python mcp_client.py session < commands.txt

Environment variables (set via config.sh or directly):
  FLAUI_SSH_HOST    - SSH target (default: windows-vm)
  FLAUI_DLL_PATH    - Path to FlaUI.Mcp.dll on Windows
  FLAUI_MCP_TIMEOUT - Timeout in seconds (default: 30)
"""

import asyncio
import json
import os
import sys

from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client


def get_server_params() -> StdioServerParameters:
    """Build StdioServerParameters for SSH connection to the MCP server."""
    host = os.environ.get("FLAUI_SSH_HOST", "windows-vm")
    dll_path = os.environ.get(
        "FLAUI_DLL_PATH",
        "C:/Users/Joe/github/repos/FlaUI-MCP/src/FlaUI.Mcp/bin/Debug/net10.0-windows/FlaUI.Mcp.dll",
    )
    return StdioServerParameters(
        command="ssh",
        args=[
            "-o", "ServerAliveInterval=30",
            "-o", "ServerAliveCountMax=3",
            host,
            "dotnet", dll_path,
        ],
    )


async def list_tools(session: ClientSession) -> list[dict]:
    """List all tools from the MCP server. Returns list of tool dicts."""
    result = await session.list_tools()
    tools = []
    for tool in result.tools:
        tools.append({
            "name": tool.name,
            "description": tool.description,
        })
    return tools


async def call_tool(session: ClientSession, name: str, arguments: dict | None = None) -> dict:
    """Call a tool and return the response as a dict."""
    result = await session.call_tool(name, arguments=arguments or {})
    contents = []
    for item in result.content:
        entry = {"type": item.type}
        if item.type == "text":
            entry["text"] = item.text
            # Try to parse JSON text for structured output
            try:
                entry["parsed"] = json.loads(item.text)
            except (json.JSONDecodeError, TypeError):
                pass
        elif item.type == "image":
            entry["mimeType"] = getattr(item, "mimeType", None)
            entry["data_length"] = len(item.data) if getattr(item, "data", None) else 0
        elif item.type == "resource":
            entry["uri"] = getattr(item, "uri", None)
            entry["mimeType"] = getattr(item, "mimeType", None)
        contents.append(entry)
    return {
        "isError": result.isError,
        "content": contents,
    }


async def run_list():
    """One-shot: connect, list tools, print, disconnect."""
    params = get_server_params()
    async with stdio_client(params) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            tools = await list_tools(session)
            print(json.dumps(tools, indent=2))
            return tools


async def run_call(tool_name: str, arguments: dict | None = None):
    """One-shot: connect, call one tool, print response, disconnect."""
    params = get_server_params()
    async with stdio_client(params) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await call_tool(session, tool_name, arguments)
            print(json.dumps(result, indent=2))
            return result


async def run_session():
    """Interactive session: read commands from stdin, keep connection open.

    Commands (one per line):
      list
      call <tool_name> [json_arguments]
      quit
    """
    params = get_server_params()
    async with stdio_client(params) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            print("SESSION_READY", flush=True)

            for line in sys.stdin:
                line = line.strip()
                if not line or line.startswith("#"):
                    continue
                if line == "quit":
                    break

                parts = line.split(None, 2)
                cmd = parts[0]

                if cmd == "list":
                    tools = await list_tools(session)
                    print(json.dumps(tools, indent=2), flush=True)
                elif cmd == "call" and len(parts) >= 2:
                    tool_name = parts[1]
                    arguments = None
                    if len(parts) == 3:
                        try:
                            arguments = json.loads(parts[2])
                        except json.JSONDecodeError as e:
                            print(json.dumps({"error": f"Invalid JSON: {e}"}), flush=True)
                            continue
                    result = await call_tool(session, tool_name, arguments)
                    print(json.dumps(result, indent=2), flush=True)
                else:
                    print(json.dumps({"error": f"Unknown command: {line}"}), flush=True)

                print("COMMAND_DONE", flush=True)


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)

    mode = sys.argv[1]

    if mode == "list":
        asyncio.run(run_list())
    elif mode == "call":
        if len(sys.argv) < 3:
            print("Usage: mcp_client.py call <tool_name> [json_arguments]", file=sys.stderr)
            sys.exit(1)
        tool_name = sys.argv[2]
        arguments = None
        if len(sys.argv) >= 4:
            try:
                arguments = json.loads(sys.argv[3])
            except json.JSONDecodeError as e:
                print(f"Invalid JSON argument: {e}", file=sys.stderr)
                sys.exit(1)
        asyncio.run(run_call(tool_name, arguments))
    elif mode == "session":
        asyncio.run(run_session())
    else:
        print(f"Unknown mode: {mode}", file=sys.stderr)
        print(__doc__)
        sys.exit(1)


if __name__ == "__main__":
    main()
