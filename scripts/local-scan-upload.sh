#!/bin/bash
# =============================================================================
# MCP Local Scan & Upload Script
# =============================================================================
# This script helps developers:
# 1. Run mcp-scan on a local MCP server
# 2. Register the server in the governance registry (if new)
# 3. Upload the scan results
#
# Usage:
#   ./local-scan-upload.sh [config-file] [options]
#
# Examples:
#   ./local-scan-upload.sh                    # Use Claude Desktop config
#   ./local-scan-upload.sh my-server.json     # Use custom config
#   ./local-scan-upload.sh --server-id abc123 # Upload to existing server
#
# Environment Variables:
#   MCP_GATEWAY_URL  - Gateway API URL (default: http://localhost:8000)
#   MCP_TOKEN        - Authentication token (required)
# =============================================================================

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
GATEWAY_URL="${MCP_GATEWAY_URL:-http://localhost:8000}"
TOKEN="${MCP_TOKEN:-}"
PIP_INDEX_URL="${MCP_PIP_INDEX_URL:-}"  # Optional: Artifactory URL
CONFIG_FILE=""
SERVER_ID=""
SERVER_NAME=""
OWNER_TEAM=""
SKIP_REGISTER=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --server-id)
            SERVER_ID="$2"
            SKIP_REGISTER=true
            shift 2
            ;;
        --name)
            SERVER_NAME="$2"
            shift 2
            ;;
        --team)
            OWNER_TEAM="$2"
            shift 2
            ;;
        --gateway)
            GATEWAY_URL="$2"
            shift 2
            ;;
        --token)
            TOKEN="$2"
            shift 2
            ;;
        --pip-index)
            PIP_INDEX_URL="$2"
            shift 2
            ;;
        --help|-h)
            echo "Usage: $0 [config-file] [options]"
            echo ""
            echo "Options:"
            echo "  --server-id ID    Upload to existing server (skip registration)"
            echo "  --name NAME       Server name for registration"
            echo "  --team TEAM       Owner team for registration"
            echo "  --gateway URL     Gateway API URL"
            echo "  --token TOKEN     Authentication token"
            echo "  --pip-index URL   Artifactory/PyPI index URL for mcp-scan install"
            echo "  --help            Show this help"
            exit 0
            ;;
        *)
            CONFIG_FILE="$1"
            shift
            ;;
    esac
done

# Detect default config file
if [[ -z "$CONFIG_FILE" ]]; then
    if [[ "$OSTYPE" == "darwin"* ]]; then
        CONFIG_FILE="$HOME/Library/Application Support/Claude/claude_desktop_config.json"
    elif [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "win32" ]]; then
        CONFIG_FILE="$APPDATA/Claude/claude_desktop_config.json"
    else
        CONFIG_FILE="$HOME/.config/Claude/claude_desktop_config.json"
    fi
fi

echo -e "${BLUE}╔═══════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║         MCP Local Scan & Upload Tool                      ║${NC}"
echo -e "${BLUE}╚═══════════════════════════════════════════════════════════╝${NC}"
echo ""

# Check prerequisites
echo -e "${YELLOW}[1/5] Checking prerequisites...${NC}"

if ! command -v mcp-scan &> /dev/null; then
    echo -e "${YELLOW}mcp-scan is not installed. Attempting to install...${NC}"
    
    if [[ -n "$PIP_INDEX_URL" ]]; then
        echo "Installing from: $PIP_INDEX_URL"
        pip install mcp-scan --index-url "$PIP_INDEX_URL" || {
            echo -e "${RED}Error: Failed to install mcp-scan from Artifactory${NC}"
            echo ""
            echo "Try manually:"
            echo "  pip install mcp-scan --index-url $PIP_INDEX_URL"
            exit 1
        }
    else
        echo -e "${RED}Error: mcp-scan is not installed${NC}"
        echo ""
        echo "Install from your company Artifactory:"
        echo "  pip install mcp-scan --index-url https://artifactory.yourcompany.com/api/pypi/python-repo/simple"
        echo ""
        echo "Or set MCP_PIP_INDEX_URL and re-run this script to auto-install:"
        echo "  export MCP_PIP_INDEX_URL='https://artifactory.yourcompany.com/api/pypi/python-repo/simple'"
        exit 1
    fi
fi

if ! command -v jq &> /dev/null; then
    echo -e "${YELLOW}Warning: jq is not installed. JSON parsing will be limited.${NC}"
fi

if ! command -v curl &> /dev/null; then
    echo -e "${RED}Error: curl is not installed${NC}"
    exit 1
fi

if [[ -z "$TOKEN" ]]; then
    echo -e "${RED}Error: MCP_TOKEN environment variable is required${NC}"
    echo ""
    echo "Get a token from the MCP Jurisdiction portal and set it:"
    echo "  export MCP_TOKEN='your-token-here'"
    exit 1
fi

echo -e "${GREEN}✓ All prerequisites met${NC}"

# Check config file
echo ""
echo -e "${YELLOW}[2/5] Loading configuration...${NC}"

if [[ ! -f "$CONFIG_FILE" ]]; then
    echo -e "${RED}Error: Config file not found: $CONFIG_FILE${NC}"
    echo ""
    echo "Create a config file or specify one:"
    echo "  $0 path/to/config.json"
    exit 1
fi

echo "Config file: $CONFIG_FILE"

# List servers in config
if command -v jq &> /dev/null; then
    SERVERS=$(jq -r '.mcpServers | keys[]' "$CONFIG_FILE" 2>/dev/null || echo "")
    if [[ -n "$SERVERS" ]]; then
        echo "Servers found:"
        echo "$SERVERS" | while read -r server; do
            echo "  - $server"
        done
    fi
fi

# Run the scan
echo ""
echo -e "${YELLOW}[3/5] Running MCP-Scan...${NC}"
echo "This may take a minute..."

SCAN_OUTPUT=$(mktemp)
SCAN_LOG=$(mktemp)

if mcp-scan scan --config "$CONFIG_FILE" --output-format json > "$SCAN_OUTPUT" 2> "$SCAN_LOG"; then
    echo -e "${GREEN}✓ Scan completed successfully${NC}"
else
    echo -e "${RED}Scan failed. Error log:${NC}"
    cat "$SCAN_LOG"
    rm -f "$SCAN_OUTPUT" "$SCAN_LOG"
    exit 1
fi

# Parse scan results
if command -v jq &> /dev/null; then
    RISK_SCORE=$(jq -r '.risk_score // 0' "$SCAN_OUTPUT")
    TOOL_COUNT=$(jq -r '.servers[0].tools | length // 0' "$SCAN_OUTPUT")
    ISSUE_COUNT=$(jq -r '.issues | length // 0' "$SCAN_OUTPUT")
    
    echo ""
    echo "Scan Results:"
    echo "  Risk Score: $RISK_SCORE"
    echo "  Tools Found: $TOOL_COUNT"
    echo "  Issues: $ISSUE_COUNT"
fi

# Register server if needed
if [[ "$SKIP_REGISTER" == "false" ]]; then
    echo ""
    echo -e "${YELLOW}[4/5] Registering server...${NC}"
    
    # Get server name from user if not provided
    if [[ -z "$SERVER_NAME" ]]; then
        if command -v jq &> /dev/null; then
            FIRST_SERVER=$(jq -r '.mcpServers | keys[0]' "$CONFIG_FILE" 2>/dev/null || echo "my-local-server")
        else
            FIRST_SERVER="my-local-server"
        fi
        
        read -p "Server name [$FIRST_SERVER]: " SERVER_NAME
        SERVER_NAME="${SERVER_NAME:-$FIRST_SERVER}"
    fi
    
    # Get owner team if not provided
    if [[ -z "$OWNER_TEAM" ]]; then
        DEFAULT_TEAM=$(whoami)
        read -p "Owner team [$DEFAULT_TEAM]: " OWNER_TEAM
        OWNER_TEAM="${OWNER_TEAM:-$DEFAULT_TEAM}"
    fi
    
    CANONICAL_ID="local/${SERVER_NAME}"
    
    # Get declared tools from scan
    DECLARED_TOOLS="[]"
    if command -v jq &> /dev/null; then
        DECLARED_TOOLS=$(jq -c '[.servers[0].tools[].name] // []' "$SCAN_OUTPUT" 2>/dev/null || echo "[]")
    fi
    
    # Register the server
    REGISTER_RESPONSE=$(curl -s -X POST "${GATEWAY_URL}/registry/servers" \
        -H "Authorization: Bearer ${TOKEN}" \
        -H "Content-Type: application/json" \
        -d "{
            \"canonicalId\": \"${CANONICAL_ID}\",
            \"name\": \"${SERVER_NAME}\",
            \"ownerTeam\": \"${OWNER_TEAM}\",
            \"sourceType\": \"LocalDeclared\",
            \"declaredTools\": ${DECLARED_TOOLS},
            \"mcpConfig\": {
                \"transport\": \"stdio\",
                \"configFile\": \"${CONFIG_FILE}\"
            }
        }")
    
    # Extract server ID
    if command -v jq &> /dev/null; then
        SERVER_ID=$(echo "$REGISTER_RESPONSE" | jq -r '.id // empty')
        ERROR_MSG=$(echo "$REGISTER_RESPONSE" | jq -r '.error // empty')
    fi
    
    if [[ -z "$SERVER_ID" ]]; then
        echo -e "${RED}Failed to register server${NC}"
        echo "Response: $REGISTER_RESPONSE"
        rm -f "$SCAN_OUTPUT" "$SCAN_LOG"
        exit 1
    fi
    
    echo -e "${GREEN}✓ Server registered: $SERVER_ID${NC}"
else
    echo ""
    echo -e "${YELLOW}[4/5] Using existing server: $SERVER_ID${NC}"
fi

# Upload scan results
echo ""
echo -e "${YELLOW}[5/5] Uploading scan results...${NC}"

SCAN_VERSION=$(mcp-scan --version 2>/dev/null | head -1 || echo "unknown")
SCANNED_AT=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

# Escape JSON for upload
SCAN_JSON=$(cat "$SCAN_OUTPUT")

UPLOAD_RESPONSE=$(curl -s -X POST "${GATEWAY_URL}/registry/servers/${SERVER_ID}/scan/upload" \
    -H "Authorization: Bearer ${TOKEN}" \
    -H "Content-Type: application/json" \
    -d "{
        \"scanOutput\": $(echo "$SCAN_JSON" | jq -Rs .),
        \"scanVersion\": \"${SCAN_VERSION}\",
        \"scannedAt\": \"${SCANNED_AT}\"
    }")

if command -v jq &> /dev/null; then
    UPLOAD_ID=$(echo "$UPLOAD_RESPONSE" | jq -r '.id // empty')
    NEW_STATUS=$(echo "$UPLOAD_RESPONSE" | jq -r '.status // empty')
fi

if [[ -n "$UPLOAD_ID" ]]; then
    echo -e "${GREEN}✓ Scan uploaded successfully${NC}"
else
    echo -e "${RED}Failed to upload scan results${NC}"
    echo "Response: $UPLOAD_RESPONSE"
    rm -f "$SCAN_OUTPUT" "$SCAN_LOG"
    exit 1
fi

# Cleanup
rm -f "$SCAN_OUTPUT" "$SCAN_LOG"

# Summary
echo ""
echo -e "${GREEN}╔═══════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║                    Scan Complete!                         ║${NC}"
echo -e "${GREEN}╚═══════════════════════════════════════════════════════════╝${NC}"
echo ""
echo "Server ID:    $SERVER_ID"
echo "Risk Score:   ${RISK_SCORE:-N/A}"
echo "Status:       ${NEW_STATUS:-Pending}"
echo ""
echo "Next steps:"
if [[ "${RISK_SCORE:-0}" -le 50 ]]; then
    echo "  ✓ Scan passed! Request admin approval."
else
    echo "  ⚠ High risk score. Review issues before requesting approval."
fi
echo ""
echo "View in portal: ${GATEWAY_URL/api./}/dashboard/servers/${SERVER_ID}"
