#!/usr/bin/env bash
# Remote build on Windows VM via SSH.
# Runs dotnet build, passes through output, propagates exit code.
#
# Usage:
#   ./test/build.sh              # Debug build (default)
#   ./test/build.sh Release      # Release build
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/config.sh"

CONFIG="${1:-Debug}"

echo "=== Building FlaUI-MCP on $FLAUI_SSH_HOST (Configuration: $CONFIG) ==="

ssh -o ConnectTimeout="$FLAUI_SSH_TIMEOUT" "$FLAUI_SSH_HOST" \
    "dotnet build '$FLAUI_PROJECT_PATH' --configuration '$CONFIG'" 2>&1

EXIT_CODE=$?

if [ $EXIT_CODE -eq 0 ]; then
    echo "=== Build succeeded ==="
else
    echo "=== Build FAILED (exit code: $EXIT_CODE) ==="
fi

exit $EXIT_CODE
