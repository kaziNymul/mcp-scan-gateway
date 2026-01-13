# Container-Based MCP Server Registration

This example demonstrates how to register containerized MCP servers from a container registry.

## Supported Source Types

| Source Type | Description | Example |
|------------|-------------|---------|
| `ContainerImage` | OCI container image | `ghcr.io/org/mcp-server:v1.0` |
| `ExternalRepo` | Public git repository | `github.com/org/mcp-server` |
| `InternalRepo` | Private git repository | `git.company.com/team/mcp-server` |
| `PackageArtifact` | Published package | `npm:@org/mcp-server` |

## Example: Register from Container Image

```bash
GATEWAY_URL="https://mcp-jurisdiction.example.com"
TOKEN="your-jwt-token"

curl -X POST "${GATEWAY_URL}/registry/servers" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "canonicalId": "ghcr.io/invariantlabs-ai/mcp-example:v1.0.0",
    "name": "Invariant Labs Example Server",
    "ownerTeam": "security-team",
    "sourceType": "ContainerImage",
    "declaredTools": [
      "code_search",
      "vulnerability_scan",
      "dependency_check"
    ],
    "mcpConfig": {
      "image": "ghcr.io/invariantlabs-ai/mcp-example:v1.0.0",
      "transport": "stdio",
      "command": ["python", "-m", "mcp_example"],
      "env": {
        "LOG_LEVEL": "INFO"
      },
      "resources": {
        "cpu": "200m",
        "memory": "256Mi"
      }
    }
  }'
```

## Example: Register from Git Repository

```bash
curl -X POST "${GATEWAY_URL}/registry/servers" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "canonicalId": "github.com/modelcontextprotocol/servers/filesystem",
    "name": "MCP Filesystem Server",
    "ownerTeam": "platform-team",
    "sourceType": "ExternalRepo",
    "declaredTools": [
      "read_file",
      "write_file",
      "list_directory",
      "create_directory",
      "move_file",
      "search_files"
    ],
    "mcpConfig": {
      "repository": "https://github.com/modelcontextprotocol/servers.git",
      "branch": "main",
      "path": "src/filesystem",
      "buildCommand": "npm install && npm run build",
      "startCommand": "node dist/index.js"
    }
  }'
```

## Scan Process for Container Images

When you trigger a scan for a container image, the gateway:

1. Creates a Kubernetes Job with the MCP-Scan image
2. The job pulls the target container image
3. MCP-Scan performs:
   - **Static analysis**: Tool manifests, dependency vulnerabilities
   - **Dynamic testing**: Fuzzes tool inputs for prompt injection
   - **Schema validation**: Checks MCP protocol compliance
4. Results are stored in the registry database

### Scan Job Example

The gateway creates a job similar to:

```yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: mcp-scan-550e8400
  namespace: mcp-jurisdiction
spec:
  ttlSecondsAfterFinished: 3600
  template:
    spec:
      serviceAccountName: mcp-jurisdiction-scanner
      containers:
        - name: scanner
          image: invariantlabs/mcp-scan:latest
          args:
            - "scan"
            - "--config"
            - "/tmp/config.json"
            - "--output-format"
            - "json"
          env:
            - name: MCP_SERVER_IMAGE
              value: "ghcr.io/invariantlabs-ai/mcp-example:v1.0.0"
      restartPolicy: Never
```

## Viewing Scan Results

```bash
# Get scan details
SERVER_ID="550e8400-e29b-41d4-a716-446655440000"
SCAN_ID="660e8400-e29b-41d4-a716-446655440001"

curl "${GATEWAY_URL}/registry/servers/${SERVER_ID}/scans/${SCAN_ID}" \
  -H "Authorization: Bearer ${TOKEN}" | jq .
```

Response:
```json
{
  "id": "660e8400-e29b-41d4-a716-446655440001",
  "serverId": "550e8400-e29b-41d4-a716-446655440000",
  "riskScore": 25,
  "status": "Completed",
  "startedAt": "2024-01-15T10:31:00Z",
  "completedAt": "2024-01-15T10:33:15Z",
  "issues": [
    {
      "severity": "low",
      "type": "dependency",
      "message": "Outdated dependency: requests==2.28.0 (recommend 2.31.0)"
    }
  ],
  "discoveredTools": [
    {
      "name": "code_search",
      "inputSchema": {...},
      "description": "Search code in the repository"
    }
  ]
}
```

## Batch Registration

For registering multiple servers, use the bulk endpoint:

```bash
curl -X POST "${GATEWAY_URL}/registry/servers/bulk" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "servers": [
      {
        "canonicalId": "internal/server-1",
        "name": "Server 1",
        "ownerTeam": "team-a",
        "sourceType": "ContainerImage",
        "mcpConfig": {...}
      },
      {
        "canonicalId": "internal/server-2",
        "name": "Server 2",
        "ownerTeam": "team-b",
        "sourceType": "ContainerImage",
        "mcpConfig": {...}
      }
    ],
    "autoScan": true
  }'
```

## CI/CD Integration

Add server registration to your CI/CD pipeline:

```yaml
# .github/workflows/register-mcp-server.yml
name: Register MCP Server

on:
  push:
    tags:
      - 'v*'

jobs:
  register:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Build and push container
        run: |
          docker build -t ghcr.io/${{ github.repository }}:${{ github.ref_name }} .
          docker push ghcr.io/${{ github.repository }}:${{ github.ref_name }}
      
      - name: Register with MCP Jurisdiction
        run: |
          curl -X POST "${{ secrets.MCP_GATEWAY_URL }}/registry/servers" \
            -H "Authorization: Bearer ${{ secrets.MCP_TOKEN }}" \
            -H "Content-Type: application/json" \
            -d '{
              "canonicalId": "ghcr.io/${{ github.repository }}:${{ github.ref_name }}",
              "name": "${{ github.event.repository.name }}",
              "ownerTeam": "${{ github.repository_owner }}",
              "sourceType": "ContainerImage",
              "mcpConfig": {
                "image": "ghcr.io/${{ github.repository }}:${{ github.ref_name }}"
              }
            }'
```
