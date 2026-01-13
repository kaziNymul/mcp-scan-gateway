#!/bin/bash
# Example script to register and approve a local MCP server
# 
# Usage: ./register.sh <server-name> <endpoint-url>
#
# Prerequisites:
# - MCP_GATEWAY_URL environment variable set
# - MCP_TOKEN environment variable set (JWT or Azure AD token)

set -euo pipefail

SERVER_NAME="${1:-my-local-server}"
ENDPOINT_URL="${2:-http://localhost:3000/sse}"

GATEWAY_URL="${MCP_GATEWAY_URL:-http://localhost:8000}"
TOKEN="${MCP_TOKEN:-}"

if [[ -z "$TOKEN" ]]; then
    echo "Error: MCP_TOKEN environment variable is required"
    exit 1
fi

echo "=== Registering MCP Server: ${SERVER_NAME} ==="
echo "Gateway URL: ${GATEWAY_URL}"
echo "Endpoint: ${ENDPOINT_URL}"
echo ""

# Generate a canonical ID
CANONICAL_ID="local/${SERVER_NAME}"

# Step 1: Register the server
echo "Step 1: Registering server..."
RESPONSE=$(curl -s -X POST "${GATEWAY_URL}/registry/servers" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d "{
    \"canonicalId\": \"${CANONICAL_ID}\",
    \"name\": \"${SERVER_NAME}\",
    \"ownerTeam\": \"$(whoami)\",
    \"sourceType\": \"LocalDeclared\",
    \"declaredTools\": [],
    \"mcpConfig\": {
      \"transport\": \"sse\",
      \"url\": \"${ENDPOINT_URL}\",
      \"timeout\": 30000
    }
  }")

SERVER_ID=$(echo "$RESPONSE" | jq -r '.id')

if [[ "$SERVER_ID" == "null" ]] || [[ -z "$SERVER_ID" ]]; then
    echo "Error: Failed to register server"
    echo "Response: $RESPONSE"
    exit 1
fi

echo "Registered with ID: ${SERVER_ID}"
echo ""

# Step 2: Trigger security scan
echo "Step 2: Triggering security scan..."
curl -s -X POST "${GATEWAY_URL}/registry/servers/${SERVER_ID}/scan" \
  -H "Authorization: Bearer ${TOKEN}" | jq .

echo ""

# Step 3: Wait for scan to complete
echo "Step 3: Waiting for scan to complete..."
for i in {1..30}; do
    STATUS=$(curl -s "${GATEWAY_URL}/registry/servers/${SERVER_ID}" \
      -H "Authorization: Bearer ${TOKEN}" | jq -r '.status')
    
    echo "  Status: ${STATUS}"
    
    if [[ "$STATUS" == "ScannedPass" ]] || [[ "$STATUS" == "ScannedFail" ]]; then
        break
    fi
    
    if [[ "$STATUS" == "Scanning" ]] || [[ "$STATUS" == "PendingScan" ]]; then
        sleep 5
    else
        echo "Unexpected status: ${STATUS}"
        break
    fi
done

echo ""

# Step 4: Show final status
echo "Step 4: Final server status:"
curl -s "${GATEWAY_URL}/registry/servers/${SERVER_ID}" \
  -H "Authorization: Bearer ${TOKEN}" | jq .

echo ""
echo "=== Registration Complete ==="
echo ""
echo "To approve this server (admin only):"
echo "  curl -X POST '${GATEWAY_URL}/registry/servers/${SERVER_ID}/approve' \\"
echo "    -H 'Authorization: Bearer \$ADMIN_TOKEN' \\"
echo "    -H 'Content-Type: application/json' \\"
echo "    -d '{\"justification\": \"Approved for development\"}'"
