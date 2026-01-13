# Local MCP Server Registration Example

This example demonstrates how to register a locally-declared MCP server with the Jurisdiction Registry.

## When to Use This Pattern

Use this pattern when:
- You have an MCP server running locally (development) that you want to proxy through the gateway
- You're building a proof-of-concept and need quick registration
- You want to test the governance flow before containerizing

## ⚠️ Important Limitations

**Do NOT claim this solution can:**
- "Scan laptops and detect all MCP servers"
- "Automatically discover MCP servers on the network"
- "Enforce policy on direct MCP connections"

**This solution ONLY governs traffic that flows through the gateway.**

## Prerequisites

1. MCP Jurisdiction Gateway deployed and accessible
2. Valid authentication token (JWT or Azure AD)
3. An MCP server endpoint reachable from the gateway

## Step 1: Register the Server

```bash
# Set your gateway URL and token
GATEWAY_URL="https://mcp-jurisdiction.example.com"
TOKEN="your-jwt-token-here"

# Register the local server
curl -X POST "${GATEWAY_URL}/registry/servers" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "canonicalId": "local/my-dev-server",
    "name": "My Development MCP Server",
    "ownerTeam": "platform-team",
    "sourceType": "LocalDeclared",
    "declaredTools": [
      "file_read",
      "file_write",
      "search_codebase"
    ],
    "mcpConfig": {
      "transport": "sse",
      "url": "http://localhost:3000/sse",
      "timeout": 30000
    }
  }'
```

Response:
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "canonicalId": "local/my-dev-server",
  "name": "My Development MCP Server",
  "status": "Draft",
  "createdAt": "2024-01-15T10:30:00Z"
}
```

## Step 2: Trigger a Security Scan

```bash
# Trigger scan for the registered server
curl -X POST "${GATEWAY_URL}/registry/servers/550e8400-e29b-41d4-a716-446655440000/scan" \
  -H "Authorization: Bearer ${TOKEN}"
```

Response:
```json
{
  "serverId": "550e8400-e29b-41d4-a716-446655440000",
  "scanJobName": "mcp-scan-550e8400",
  "status": "Scanning",
  "estimatedDurationSeconds": 120
}
```

## Step 3: Check Scan Status

```bash
# Poll for scan completion
curl "${GATEWAY_URL}/registry/servers/550e8400-e29b-41d4-a716-446655440000" \
  -H "Authorization: Bearer ${TOKEN}"
```

Response when scan completes:
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "ScannedPass",
  "latestRiskScore": 25,
  "latestScanId": "660e8400-e29b-41d4-a716-446655440001"
}
```

## Step 4: Approve the Server (Admin Only)

```bash
# Approve the server (requires admin role)
curl -X POST "${GATEWAY_URL}/registry/servers/550e8400-e29b-41d4-a716-446655440000/approve" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "justification": "Reviewed scan results, acceptable risk for development use",
    "approvalScope": "development"
  }'
```

## Step 5: Use the Server Through Gateway

Once approved, clients can connect through the gateway:

```bash
# MCP endpoint proxied through gateway
curl -X POST "${GATEWAY_URL}/mcp/local/my-dev-server/call" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "method": "tools/call",
    "params": {
      "name": "file_read",
      "arguments": {
        "path": "/src/main.py"
      }
    }
  }'
```

## Local Development Adapter

For local development, you can use an SSE adapter that bridges your local server:

```python
# local_adapter.py
import asyncio
from mcp import Server, StdioServerParameters
import httpx

class LocalAdapter:
    """Bridges a local stdio MCP server to SSE for gateway compatibility."""
    
    def __init__(self, local_command: list[str], port: int = 3000):
        self.local_command = local_command
        self.port = port
    
    async def run(self):
        # Start local MCP server
        process = await asyncio.create_subprocess_exec(
            *self.local_command,
            stdin=asyncio.subprocess.PIPE,
            stdout=asyncio.subprocess.PIPE,
        )
        
        # Expose as SSE endpoint on localhost
        from starlette.applications import Starlette
        from starlette.routing import Route
        from sse_starlette.sse import EventSourceResponse
        
        async def sse_endpoint(request):
            async def event_generator():
                while True:
                    line = await process.stdout.readline()
                    if line:
                        yield {"data": line.decode()}
            return EventSourceResponse(event_generator())
        
        app = Starlette(routes=[Route("/sse", sse_endpoint)])
        
        import uvicorn
        await uvicorn.Server(
            uvicorn.Config(app, host="0.0.0.0", port=self.port)
        ).serve()

if __name__ == "__main__":
    adapter = LocalAdapter(["python", "my_mcp_server.py"])
    asyncio.run(adapter.run())
```

## Next Steps

1. Review the [Security Documentation](../../docs/security.md) for production hardening
2. See [Container Registration](../container-server/README.md) for containerized servers
3. Configure [Team Allowlists](../../docs/policy-configuration.md) for fine-grained access
