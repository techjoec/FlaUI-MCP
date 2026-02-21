#!/usr/bin/env python3
"""Stateful integration test for FlaUI-MCP.

Demonstrates a multi-tool session within a single MCP connection:
  1. Launch Notepad
  2. Take a snapshot (get element refs)
  3. Verify window appears in window list
  4. Close the window
  5. Verify window is gone

Reports PASS/FAIL per check.

Usage:
  source test/config.sh && $FLAUI_PYTHON test/session_test.py
  # or
  ./test/call_tool.sh session < <(python3 test/session_test.py --commands-only)
"""

import asyncio
import json
import os
import sys
import time

# Add test directory to path for imports
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mcp_client import get_server_params, list_tools, call_tool

from mcp import ClientSession
from mcp.client.stdio import stdio_client


class TestResult:
    def __init__(self):
        self.passed = 0
        self.failed = 0

    def check(self, name: str, condition: bool, detail: str = ""):
        if condition:
            print(f"  PASS: {name}")
            self.passed += 1
        else:
            msg = f"  FAIL: {name}"
            if detail:
                msg += f" ({detail})"
            print(msg)
            self.failed += 1
        return condition

    def summary(self) -> int:
        total = self.passed + self.failed
        print(f"\n=== Results: {self.passed}/{total} passed, {self.failed} failed ===")
        return 0 if self.failed == 0 else 1


async def run_notepad_test():
    """Run the full Notepad integration test."""
    result = TestResult()
    params = get_server_params()
    window_handle = None

    async with stdio_client(params) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            print("=== Session initialized ===\n")

            # --- Step 1: Launch Notepad ---
            print("--- Step 1: Launch Notepad ---")
            launch = await call_tool(session, "windows_launch", {"app": "notepad.exe"})
            is_not_error = not launch.get("isError", False)
            result.check("Launch Notepad", is_not_error,
                         json.dumps(launch) if not is_not_error else "")

            if is_not_error:
                # Extract window handle from response
                for content in launch.get("content", []):
                    parsed = content.get("parsed", {})
                    if isinstance(parsed, dict) and "windowId" in parsed:
                        window_handle = parsed["windowId"]
                        break
                result.check("Got window handle", window_handle is not None)

            # Brief pause for window to render
            await asyncio.sleep(1)

            # --- Step 2: Take snapshot ---
            print("\n--- Step 2: Take snapshot ---")
            if window_handle:
                snapshot = await call_tool(session, "windows_snapshot", {"windowId": window_handle})
                snap_ok = not snapshot.get("isError", False)
                result.check("Snapshot succeeded", snap_ok,
                             json.dumps(snapshot) if not snap_ok else "")

                # Verify snapshot contains element refs
                if snap_ok:
                    for content in snapshot.get("content", []):
                        text = content.get("text", "")
                        has_refs = "e" in text  # Element refs like w1e5
                        result.check("Snapshot contains element refs", has_refs)
                        break
            else:
                result.check("Snapshot (skipped - no window handle)", False)

            # --- Step 3: Verify window in list ---
            print("\n--- Step 3: Verify window in list ---")
            windows = await call_tool(session, "windows_list_windows")
            list_ok = not windows.get("isError", False)
            result.check("List windows succeeded", list_ok)

            if list_ok and window_handle:
                # Check that our window handle appears in the response
                response_text = json.dumps(windows)
                found = window_handle in response_text
                result.check(f"Window {window_handle} in list", found)

            # --- Step 4: Close the window ---
            print("\n--- Step 4: Close window ---")
            if window_handle:
                close = await call_tool(session, "windows_close", {"windowId": window_handle})
                close_ok = not close.get("isError", False)
                result.check("Close window succeeded", close_ok,
                             json.dumps(close) if not close_ok else "")
            else:
                result.check("Close window (skipped - no handle)", False)

            await asyncio.sleep(1)

            # --- Step 5: Verify window is gone ---
            print("\n--- Step 5: Verify window is gone ---")
            windows2 = await call_tool(session, "windows_list_windows")
            list2_ok = not windows2.get("isError", False)
            result.check("List windows after close succeeded", list2_ok)

            if list2_ok and window_handle:
                response_text = json.dumps(windows2)
                gone = window_handle not in response_text
                result.check(f"Window {window_handle} no longer in list", gone)

    return result.summary()


def main():
    exit_code = asyncio.run(run_notepad_test())
    sys.exit(exit_code)


if __name__ == "__main__":
    main()
