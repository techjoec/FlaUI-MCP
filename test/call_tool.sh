#!/usr/bin/env bash
# Ad-hoc tool calling wrapper for FlaUI-MCP.
#
# Usage:
#   ./test/call_tool.sh list
#   ./test/call_tool.sh call windows_status
#   ./test/call_tool.sh call windows_launch '{"app":"calc.exe"}'
#   ./test/call_tool.sh session
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/config.sh"

exec "$FLAUI_PYTHON" "$SCRIPT_DIR/mcp_client.py" "$@"
