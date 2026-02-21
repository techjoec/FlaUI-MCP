#!/usr/bin/env bash
# One-command build + verify for FlaUI-MCP.
#
# Steps:
#   1. SSH connectivity check (fail fast)
#   2. Remote dotnet build (pass through output)
#   3. MCP initialize + tools/list (validate 15 tools)
#   4. Live tool call (windows_list_windows)
#
# Exit 0 on all pass, exit 1 on any failure.
#
# Usage:
#   ./test/smoke.sh
#   FLAUI_SSH_HOST=other-vm ./test/smoke.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/config.sh"

PASS=0
FAIL=0
EXPECTED_TOOLS=15

pass() { echo "  PASS: $1"; PASS=$((PASS + 1)); }
fail() { echo "  FAIL: $1"; FAIL=$((FAIL + 1)); }

# ---------- Step 1: SSH connectivity ----------
echo "=== Step 1: SSH connectivity ==="
if ssh -o ConnectTimeout="$FLAUI_SSH_TIMEOUT" -o BatchMode=yes "$FLAUI_SSH_HOST" "echo ok" >/dev/null 2>&1; then
    pass "SSH connection to $FLAUI_SSH_HOST"
else
    fail "SSH connection to $FLAUI_SSH_HOST"
    echo "Cannot continue without SSH. Exiting."
    exit 1
fi

# ---------- Step 2: Remote build ----------
echo "=== Step 2: Remote build ==="
if "$SCRIPT_DIR/build.sh" 2>&1; then
    pass "dotnet build"
else
    fail "dotnet build"
    echo "Cannot continue without a successful build. Exiting."
    exit 1
fi

# ---------- Step 3: MCP tools/list ----------
echo "=== Step 3: MCP tools/list ==="
TOOLS_JSON=$("$FLAUI_PYTHON" "$SCRIPT_DIR/mcp_client.py" list 2>/dev/null) || true

if [ -z "$TOOLS_JSON" ]; then
    fail "MCP initialize + tools/list (no response)"
else
    TOOL_COUNT=$(printf '%s' "$TOOLS_JSON" | "$FLAUI_PYTHON" -c "
import sys, json
data = sys.stdin.read()
if not data.strip():
    print(0)
else:
    print(len(json.loads(data)))
" 2>/dev/null) || TOOL_COUNT=0
    if [ "$TOOL_COUNT" -eq "$EXPECTED_TOOLS" ]; then
        pass "tools/list returned $TOOL_COUNT tools"
    else
        fail "tools/list returned $TOOL_COUNT tools (expected $EXPECTED_TOOLS)"
        echo "$TOOLS_JSON"
    fi
fi

# ---------- Step 4: Live tool call ----------
echo "=== Step 4: Live tool call (windows_list_windows) ==="
CALL_JSON=$("$FLAUI_PYTHON" "$SCRIPT_DIR/mcp_client.py" call windows_list_windows 2>/dev/null) || true

if [ -z "$CALL_JSON" ]; then
    fail "windows_list_windows (no response)"
else
    IS_ERROR=$(printf '%s' "$CALL_JSON" | "$FLAUI_PYTHON" -c "
import sys, json
data = sys.stdin.read()
if not data.strip():
    print('True')
else:
    print(json.loads(data).get('isError', False))
" 2>/dev/null) || IS_ERROR="True"
    if [ "$IS_ERROR" = "False" ]; then
        pass "windows_list_windows returned successfully"
    else
        fail "windows_list_windows returned error"
        echo "$CALL_JSON"
    fi
fi

# ---------- Summary ----------
echo ""
echo "=== Results: $PASS passed, $FAIL failed ==="

if [ "$FAIL" -gt 0 ]; then
    exit 1
fi
exit 0
