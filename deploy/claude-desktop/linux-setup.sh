#!/usr/bin/env bash
# Installs and configures SQL MCP Server for Claude Desktop on Linux/macOS.
# Usage: ./linux-setup.sh [PROJECT_PATH] [CONNECTION_STRING] [AUTH_MODE]

set -euo pipefail

PROJECT_PATH="${1:-$(pwd)}"
CONNECTION_STRING="${2:-Server=localhost;Database=master;User Id=sa;Password=YourPassword;Encrypt=false;TrustServerCertificate=true;}"
AUTH_MODE="${3:-SqlAuth}"

HOST_PROJECT="$PROJECT_PATH/src/SqlMcpServer.Host"
PUBLISH_DIR="$PROJECT_PATH/publish/linux-x64"

# Detect OS for config path
if [[ "$(uname)" == "Darwin" ]]; then
    CLAUDE_CONFIG="$HOME/Library/Application Support/Claude/claude_desktop_config.json"
else
    CLAUDE_CONFIG="$HOME/.config/Claude/claude_desktop_config.json"
fi

echo "[1/4] Checking prerequisites..."

if ! command -v dotnet &>/dev/null; then
    echo "ERROR: .NET SDK not found. Install from https://dotnet.microsoft.com/download/dotnet/8.0" >&2
    exit 1
fi

echo "[2/4] Building self-contained binary..."

dotnet publish "$HOST_PROJECT" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -o "$PUBLISH_DIR" \
    /p:PublishSingleFile=true \
    /p:UseAppHost=true

BINARY="$PUBLISH_DIR/SqlMcpServer.Host"

if [[ ! -f "$BINARY" ]]; then
    echo "ERROR: Binary not found at $BINARY" >&2
    exit 1
fi

chmod +x "$BINARY"
echo "Published to: $PUBLISH_DIR"

echo "[3/4] Writing Claude Desktop config..."

CONFIG_DIR="$(dirname "$CLAUDE_CONFIG")"
mkdir -p "$CONFIG_DIR"

# Use python3 or jq to merge JSON safely
if command -v python3 &>/dev/null; then
    python3 - "$CLAUDE_CONFIG" "$BINARY" "$CONNECTION_STRING" "$AUTH_MODE" <<'PYEOF'
import json, sys, os

config_path, binary, conn_str, auth_mode = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]

config = {"mcpServers": {}}
if os.path.exists(config_path):
    with open(config_path) as f:
        config = json.load(f)
if "mcpServers" not in config:
    config["mcpServers"] = {}

config["mcpServers"]["mssql"] = {
    "command": binary,
    "args": [],
    "env": {
        "SqlServer__ConnectionString": conn_str,
        "SqlServer__AuthMode": auth_mode,
        "ASPNETCORE_ENVIRONMENT": "Production",
        "Serilog__MinimumLevel__Default": "Warning"
    }
}

with open(config_path, "w") as f:
    json.dump(config, f, indent=2)

print(f"Config written: {config_path}")
PYEOF
else
    echo "WARNING: python3 not found — writing config from scratch (existing entries may be lost)."
    cat > "$CLAUDE_CONFIG" <<EOF
{
  "mcpServers": {
    "mssql": {
      "command": "$BINARY",
      "args": [],
      "env": {
        "SqlServer__ConnectionString": "$CONNECTION_STRING",
        "SqlServer__AuthMode": "$AUTH_MODE",
        "ASPNETCORE_ENVIRONMENT": "Production",
        "Serilog__MinimumLevel__Default": "Warning"
      }
    }
  }
}
EOF
fi

echo "[4/4] Done!"
echo ""
echo "Restart Claude Desktop to load the MCP server."
echo "Config: $CLAUDE_CONFIG"
