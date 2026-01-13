# GitHub Copilot + MCP Gateway Integration

This guide explains how to enforce the MCP Jurisdiction allowlist with GitHub Copilot.

## Overview

The MCP Gateway acts as a **proxy** between GitHub Copilot and MCP servers. When developers configure their MCP servers to go through the gateway, the gateway:

1. **Validates** the server is registered and approved
2. **Proxies** the request to the actual server
3. **Blocks** requests to unapproved servers

```
┌─────────────────┐      ┌─────────────────┐      ┌─────────────────┐
│  GitHub Copilot │ ───> │   MCP Gateway   │ ───> │   MCP Server    │
│   (VS Code)     │      │   (Allowlist)   │      │   (Approved)    │
└─────────────────┘      └─────────────────┘      └─────────────────┘
                                │
                                │ ✗ Block if not approved
                                ▼
                         ┌─────────────────┐
                         │  403 Forbidden  │
                         └─────────────────┘
```

## Configuration Methods

### Method 1: Individual Developer Configuration

Each developer configures their VS Code to use the gateway proxy.

#### For Remote/HTTP MCP Servers

In your `.vscode/mcp.json` or VS Code settings:

```json
{
  "mcp": {
    "servers": {
      "my-approved-server": {
        "type": "sse",
        "url": "https://mcp-gateway.company.com/mcp/proxy/my-server-id/sse"
      },
      "weather-api": {
        "type": "sse", 
        "url": "https://mcp-gateway.company.com/mcp/proxy/weather-server-uuid/sse"
      }
    }
  }
}
```

#### For Local/Stdio MCP Servers

Local servers run on the developer's machine and can't be proxied. Instead, use a wrapper script that checks approval first:

```json
{
  "mcp": {
    "servers": {
      "local-tool": {
        "type": "stdio",
        "command": "/usr/local/bin/mcp-checked-run",
        "args": ["my-local-server", "python", "-m", "my_mcp_server"]
      }
    }
  }
}
```

Where `mcp-checked-run` is a script that:
1. Calls the gateway's `/policy/check` endpoint
2. Only runs the server if approved

### Method 2: Organization-Wide Enforcement (Recommended)

For enterprise deployments, configure at the organization level.

#### VS Code Settings Policy (MDM/GPO)

Deploy via your MDM (Intune, JAMF) or Group Policy:

```json
{
  "github.copilot.chat.mcp.proxy": "https://mcp-gateway.company.com/mcp/proxy",
  "github.copilot.chat.mcp.allowList": [
    "https://mcp-gateway.company.com/mcp/proxy/*"
  ],
  "github.copilot.chat.mcp.enabled": true
}
```

#### VS Code Extension for Policy Enforcement

Create a VS Code extension that:
1. Intercepts MCP server configurations
2. Validates against the gateway
3. Rewrites URLs to go through the proxy

See: `deployment/vscode-mcp-policy/` (TODO)

### Method 3: Network-Level Enforcement

Block direct MCP connections at the network level:

```yaml
# Example Istio/Envoy policy
apiVersion: security.istio.io/v1beta1
kind: AuthorizationPolicy
metadata:
  name: mcp-gateway-only
spec:
  selector:
    matchLabels:
      app: developer-workstation
  rules:
  - to:
    - operation:
        hosts: ["*.mcp-server.com"]
    from:
    - source:
        principals: ["cluster.local/ns/mcp/sa/mcp-gateway"]
```

## Gateway API Endpoints

### List Approved Servers

```bash
curl https://mcp-gateway.company.com/mcp/servers
```

Response:
```json
{
  "servers": [
    {
      "id": "uuid-1234",
      "canonicalId": "github.com/org/mcp-weather",
      "name": "Weather API Server",
      "tools": ["get_weather", "get_forecast"],
      "proxyUrl": "/mcp/proxy/uuid-1234",
      "isLocal": false
    },
    {
      "id": "uuid-5678",
      "canonicalId": "python:math_server.py",
      "name": "Local Math Server",
      "tools": ["add", "multiply"],
      "proxyUrl": null,
      "isLocal": true,
      "note": "Local server - run locally"
    }
  ]
}
```

### Proxy MCP Requests

```
GET/POST https://mcp-gateway.company.com/mcp/proxy/{server-id}/*
```

The gateway:
1. Looks up the server by ID or canonical ID
2. Checks if status is "Approved"
3. Forwards the request to the actual server URL
4. Streams SSE responses for MCP protocol

### Check Policy

```bash
curl "https://mcp-gateway.company.com/policy/check?server_url=github.com/org/mcp-tool"
```

## Complete Developer Workflow

### Step 1: Browse Available Servers

Visit the MCP Jurisdiction dashboard or call the API:

```bash
curl https://mcp-gateway.company.com/mcp/servers | jq '.servers[] | {name, proxyUrl}'
```

### Step 2: Configure VS Code

Add approved servers to your workspace config:

```json
// .vscode/mcp.json
{
  "mcp": {
    "servers": {
      "weather": {
        "type": "sse",
        "url": "https://mcp-gateway.company.com/mcp/proxy/uuid-1234/sse"
      }
    }
  }
}
```

### Step 3: Use in Copilot

The MCP tools are now available in GitHub Copilot Chat:

```
@workspace Use the weather tool to get the forecast for Seattle
```

Copilot will call the MCP server through the gateway proxy.

## Handling Local Servers

Local servers (stdio-based) can't be proxied because they run on the developer's machine. Options:

### Option A: Pre-Approval Check Script

Create a wrapper that checks approval before running:

```bash
#!/bin/bash
# /usr/local/bin/mcp-checked-run
SERVER_ID="$1"
shift

# Check if approved
RESULT=$(curl -s "https://mcp-gateway.company.com/policy/check?server_url=$SERVER_ID")
ALLOWED=$(echo "$RESULT" | jq -r '.allowed')

if [ "$ALLOWED" != "true" ]; then
  echo "ERROR: MCP server '$SERVER_ID' is not approved" >&2
  exit 1
fi

# Run the actual command
exec "$@"
```

### Option B: Local Gateway Agent

Deploy a local agent that:
1. Intercepts all MCP stdio connections
2. Validates against the central gateway
3. Allows or blocks based on policy

### Option C: Trust but Verify

Allow local servers but:
1. Require registration and scan upload
2. Audit usage via telemetry
3. Alert on unregistered server usage

## Testing Locally

```bash
# Start the test gateway
docker-compose -f docker-compose.test.yml up -d

# Check available servers
curl http://localhost:8080/mcp/servers

# Try to proxy to an approved server
curl http://localhost:8080/mcp/proxy/YOUR-SERVER-ID/sse

# Try an unapproved server (should fail)
curl http://localhost:8080/mcp/proxy/unknown-server/sse
# Returns: 404 MCP server not registered
```

## Troubleshooting

### "MCP server not registered"

The server isn't in the registry. Developer needs to:
1. Register the server via the dashboard
2. Run `mcp-scan` and upload results
3. Wait for approval (or auto-approval if low risk)

### "MCP server is not approved"

The server is registered but not approved. Check:
- Dashboard for the current status (Pending/Rejected)
- Contact security team for approval

### "Cannot connect to MCP server"

The gateway can't reach the actual server. Check:
- Server URL in the registration is correct
- Server is running and accessible from the gateway
- Network policies allow gateway-to-server traffic

### SSE Connection Drops

For long-running SSE connections:
- Increase gateway timeout settings
- Check load balancer idle timeout
- Use WebSocket transport if available
