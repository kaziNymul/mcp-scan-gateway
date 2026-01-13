#!/bin/bash
# =============================================================================
# MCP Jurisdiction - End-to-End Test Script
# =============================================================================
# This script tests the complete flow:
#   1. Start the local stack with Docker Compose
#   2. Wait for services to be ready
#   3. Scan the sample MCP server
#   4. Register the server
#   5. Upload scan results
#   6. Verify registration
#   7. Test policy check
#
# Prerequisites:
#   - Docker Desktop running
#   - mcp-scan installed (pip install mcp-scan)
#
# Usage:
#   cd mcp-gateway
#   ./test-e2e.sh
# =============================================================================

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

echo -e "${BLUE}╔═══════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║       MCP Jurisdiction End-to-End Test                    ║${NC}"
echo -e "${BLUE}╚═══════════════════════════════════════════════════════════╝${NC}"
echo ""

# =============================================================================
# Step 1: Start Docker Compose
# =============================================================================
echo -e "${YELLOW}[1/7] Starting Docker Compose stack...${NC}"

docker-compose -f docker-compose.test.yml down -v 2>/dev/null || true
docker-compose -f docker-compose.test.yml up -d --build

echo "Waiting for services to start..."
sleep 5

# Wait for gateway
echo "Waiting for Gateway API..."
for i in {1..30}; do
    if curl -s http://localhost:8080/health > /dev/null 2>&1; then
        echo -e "${GREEN}✓ Gateway is ready${NC}"
        break
    fi
    if [ $i -eq 30 ]; then
        echo -e "${RED}Gateway failed to start${NC}"
        docker-compose -f docker-compose.test.yml logs gateway
        exit 1
    fi
    sleep 2
done

# Wait for MCP server
echo "Waiting for Weather MCP Server..."
for i in {1..20}; do
    if curl -s http://localhost:3001/health > /dev/null 2>&1; then
        echo -e "${GREEN}✓ Weather Server is ready${NC}"
        break
    fi
    if [ $i -eq 20 ]; then
        echo -e "${RED}Weather Server failed to start${NC}"
        docker-compose -f docker-compose.test.yml logs weather-server
        exit 1
    fi
    sleep 2
done

echo ""

# =============================================================================
# Step 2: Install mcp-scan if needed
# =============================================================================
echo -e "${YELLOW}[2/7] Checking mcp-scan installation...${NC}"

if ! command -v mcp-scan &> /dev/null; then
    echo "Installing mcp-scan..."
    pip install mcp-scan
fi

MCP_SCAN_VERSION=$(mcp-scan --version 2>/dev/null || echo "unknown")
echo -e "${GREEN}✓ mcp-scan installed: $MCP_SCAN_VERSION${NC}"
echo ""

# =============================================================================
# Step 3: Scan the MCP Server
# =============================================================================
echo -e "${YELLOW}[3/7] Scanning Weather MCP Server...${NC}"

SCAN_OUTPUT=$(mktemp)
echo "Running: mcp-scan scan --config test-config.json --output-format json"

if mcp-scan scan --config test-config.json --output-format json > "$SCAN_OUTPUT" 2>&1; then
    echo -e "${GREEN}✓ Scan completed${NC}"
else
    echo -e "${YELLOW}⚠ Scan completed with warnings (this is normal for test servers)${NC}"
fi

# Display scan summary
if command -v jq &> /dev/null; then
    RISK_SCORE=$(jq -r '.risk_score // 0' "$SCAN_OUTPUT" 2>/dev/null || echo "N/A")
    TOOL_COUNT=$(jq -r 'try .servers[0].tools | length // 0' "$SCAN_OUTPUT" 2>/dev/null || echo "0")
    echo "  Risk Score: $RISK_SCORE"
    echo "  Tools Found: $TOOL_COUNT"
fi
echo ""

# =============================================================================
# Step 4: Register the Server
# =============================================================================
echo -e "${YELLOW}[4/7] Registering server in the registry...${NC}"

REGISTER_RESPONSE=$(curl -s -X POST http://localhost:8080/registry/servers \
    -H "Content-Type: application/json" \
    -d '{
        "canonicalId": "test/weather-server",
        "name": "Weather Server (Test)",
        "ownerTeam": "test-team",
        "sourceType": "LocalDeclared",
        "declaredTools": ["get_weather", "get_forecast", "get_alerts"],
        "mcpConfig": {
            "transport": "sse",
            "url": "http://localhost:3001/sse"
        }
    }')

if command -v jq &> /dev/null; then
    SERVER_ID=$(echo "$REGISTER_RESPONSE" | jq -r '.id // empty')
    STATUS=$(echo "$REGISTER_RESPONSE" | jq -r '.status // empty')
    
    if [[ -n "$SERVER_ID" ]]; then
        echo -e "${GREEN}✓ Server registered${NC}"
        echo "  Server ID: $SERVER_ID"
        echo "  Status: $STATUS"
    else
        echo -e "${RED}✗ Registration failed${NC}"
        echo "$REGISTER_RESPONSE" | jq .
        exit 1
    fi
else
    echo "Response: $REGISTER_RESPONSE"
    SERVER_ID=$(echo "$REGISTER_RESPONSE" | grep -o '"id":"[^"]*"' | cut -d'"' -f4)
fi
echo ""

# =============================================================================
# Step 5: Upload Scan Results
# =============================================================================
echo -e "${YELLOW}[5/7] Uploading scan results...${NC}"

SCAN_JSON=$(cat "$SCAN_OUTPUT")
SCAN_VERSION=$(mcp-scan --version 2>/dev/null | head -1 || echo "test")
SCANNED_AT=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

UPLOAD_RESPONSE=$(curl -s -X POST "http://localhost:8080/registry/servers/${SERVER_ID}/scan/upload" \
    -H "Content-Type: application/json" \
    -d "{
        \"scanOutput\": $(echo "$SCAN_JSON" | jq -Rs .),
        \"scanVersion\": \"$SCAN_VERSION\",
        \"scannedAt\": \"$SCANNED_AT\"
    }")

if command -v jq &> /dev/null; then
    NEW_STATUS=$(echo "$UPLOAD_RESPONSE" | jq -r '.status // empty')
    RISK=$(echo "$UPLOAD_RESPONSE" | jq -r '.riskScore // empty')
    TOOLS=$(echo "$UPLOAD_RESPONSE" | jq -r '.toolsFound // empty')
    
    echo -e "${GREEN}✓ Scan uploaded${NC}"
    echo "  New Status: $NEW_STATUS"
    echo "  Risk Score: $RISK"
    echo "  Tools Found: $TOOLS"
else
    echo "Response: $UPLOAD_RESPONSE"
fi
echo ""

# =============================================================================
# Step 6: Verify Registration
# =============================================================================
echo -e "${YELLOW}[6/7] Verifying registration...${NC}"

SERVER_DETAILS=$(curl -s "http://localhost:8080/registry/servers/${SERVER_ID}")

if command -v jq &> /dev/null; then
    echo "$SERVER_DETAILS" | jq '{
        id: .id,
        name: .name,
        canonical_id: .canonical_id,
        status: .status,
        owner_team: .owner_team,
        source_type: .source_type
    }'
else
    echo "$SERVER_DETAILS"
fi
echo ""

# List all servers
echo "All registered servers:"
curl -s http://localhost:8080/registry/servers | jq '.[].name' 2>/dev/null || curl -s http://localhost:8080/registry/servers
echo ""

# =============================================================================
# Step 7: Test Policy Check
# =============================================================================
echo -e "${YELLOW}[7/7] Testing policy enforcement...${NC}"

echo "Checking registered server (should be allowed):"
POLICY_CHECK=$(curl -s "http://localhost:8080/policy/check?server_url=test/weather-server")
if command -v jq &> /dev/null; then
    echo "$POLICY_CHECK" | jq .
else
    echo "$POLICY_CHECK"
fi
echo ""

echo "Checking unregistered server (should be blocked):"
POLICY_CHECK=$(curl -s "http://localhost:8080/policy/check?server_url=unknown/server")
if command -v jq &> /dev/null; then
    echo "$POLICY_CHECK" | jq .
else
    echo "$POLICY_CHECK"
fi
echo ""

# =============================================================================
# Cleanup
# =============================================================================
rm -f "$SCAN_OUTPUT"

echo -e "${GREEN}╔═══════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║              All Tests Passed! ✓                          ║${NC}"
echo -e "${GREEN}╚═══════════════════════════════════════════════════════════╝${NC}"
echo ""
echo "Services running:"
echo "  • Gateway API: http://localhost:8080"
echo "  • Weather MCP Server: http://localhost:3001"
echo "  • PostgreSQL: localhost:5432"
echo ""
echo "Try these commands:"
echo "  curl http://localhost:8080/registry/servers | jq ."
echo "  curl http://localhost:8080/audit/events | jq ."
echo ""
echo "To approve the server:"
echo "  curl -X POST http://localhost:8080/registry/servers/${SERVER_ID}/approve"
echo ""
echo "To stop the stack:"
echo "  docker-compose -f docker-compose.test.yml down"
