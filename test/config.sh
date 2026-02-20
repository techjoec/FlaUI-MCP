#!/usr/bin/env bash
# Central configuration for FlaUI-MCP test scripts.
# All other test scripts source this file.
#
# Override any variable per-invocation:
#   FLAUI_SSH_HOST=other-vm ./test/smoke.sh

# SSH target (must match ~/.ssh/config alias or be user@host)
: "${FLAUI_SSH_HOST:=windows-vm}"

# Path to compiled DLL on the Windows machine (forward slashes)
: "${FLAUI_DLL_PATH:=C:/Users/Joe/github/repos/FlaUI-MCP/src/FlaUI.Mcp/bin/Debug/net10.0-windows/FlaUI.Mcp.dll}"

# Path to project root on the Windows machine (for dotnet build)
: "${FLAUI_PROJECT_PATH:=C:/Users/Joe/github/repos/FlaUI-MCP/src/FlaUI.Mcp}"

# Python interpreter (venv with mcp SDK installed)
: "${FLAUI_PYTHON:=$HOME/.venvs/mcp/bin/python}"

# SSH connection timeout in seconds
: "${FLAUI_SSH_TIMEOUT:=10}"

# MCP operation timeout in seconds
: "${FLAUI_MCP_TIMEOUT:=30}"
