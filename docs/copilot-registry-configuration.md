# GitHub Copilot Enterprise - MCP Registry Integration

This guide explains how the MCP Gateway integrates with GitHub Copilot Enterprise's policy settings.

## Overview: How It Works

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        GitHub Enterprise Cloud                               │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  Organization Settings → Copilot → Policies → MCP Registry         │    │
│  │                                                                      │    │
│  │  ┌──────────────────────────────────────────────────────────────┐   │    │
│  │  │  MCP Registry URL: https://mcp-gateway.company.com/mcp       │   │    │
│  │  └──────────────────────────────────────────────────────────────┘   │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ Copilot fetches list of allowed servers
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         MCP Gateway (Your Company)                           │
│                    https://mcp-gateway.company.com                           │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │  GET /mcp/servers → Returns list of APPROVED MCP servers only        │   │
│  │  POST /mcp/proxy/{id}/* → Proxies requests to approved servers      │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
```

## The MCP Registry URL

**Yes, the gateway URL IS the MCP Registry URL** that you configure in GitHub Copilot Enterprise.

| Configuration | URL |
|--------------|-----|
| **MCP Registry URL** | `https://mcp-gateway.company.com/mcp` |
| **What it provides** | List of approved MCP servers only |
| **What Copilot does** | Only allows connections to servers in this list |

## Gateway Endpoints for Copilot

The gateway exposes these endpoints for GitHub Copilot:

### 1. List Approved Servers (Registry Endpoint)

```
GET https://mcp-gateway.company.com/mcp/servers
```

Returns ONLY approved servers:

```json
{
  "servers": [
    {
      "id": "9ccbde7a-f552-40f9-8616-9086d93e3882",
      "canonicalId": "python:math_server.py",
      "name": "Math Operations Server",
      "description": "Basic math operations",
      "tools": ["add", "subtract", "multiply", "divide"],
      "status": "Approved",
      "proxyUrl": "https://mcp-gateway.company.com/mcp/proxy/9ccbde7a-f552-40f9-8616-9086d93e3882"
    },
    {
      "id": "abc123",
      "canonicalId": "github.com/company/weather-mcp",
      "name": "Weather API Server",
      "tools": ["get_weather", "get_forecast"],
      "status": "Approved",
      "proxyUrl": "https://mcp-gateway.company.com/mcp/proxy/abc123"
    }
  ]
}
```

**Important**: Servers with status `Pending`, `Rejected`, or `Suspended` are NOT included.

### 2. Proxy Endpoint

```
POST https://mcp-gateway.company.com/mcp/proxy/{server-id}/{path}
```

GitHub Copilot uses this to communicate with the actual MCP server through the gateway.

---

## Configuring GitHub Copilot Enterprise

### Step 1: Access Organization Settings

1. Go to your GitHub organization: `https://github.com/your-org`
2. Click **Settings** → **Copilot** → **Policies**

### Step 2: Find MCP Registry Settings

Navigate to the MCP (Model Context Protocol) section:

1. Look for **MCP Servers** or **Extensions** policy
2. Find the **MCP Registry** configuration option

### Step 3: Configure the Registry URL

| Setting | Value |
|---------|-------|
| **Registry URL** | `https://mcp-gateway.company.com/mcp` |
| **Enforcement** | `Allowed list only` (recommended) |

### Step 4: Choose Enforcement Mode

| Mode | Behavior |
|------|----------|
| **Allowed list only** | ✅ Copilot can ONLY use servers from the registry |
| **Allowed list + local** | Copilot can use registry servers + local stdio servers |
| **No restriction** | ❌ Not recommended for enterprise |

---

## The Flow in Practice

### Developer Experience

1. **Developer opens VS Code**
2. **Copilot Chat loads MCP servers from registry**:
   ```
   GET https://mcp-gateway.company.com/mcp/servers
   → Returns: [Math Server, Weather Server] (approved only)
   ```
3. **Developer uses a tool**:
   ```
   @workspace Calculate 42 + 17 using the math server
   ```
4. **Copilot calls the server through the proxy**:
   ```
   POST https://mcp-gateway.company.com/mcp/proxy/9ccbde7a.../invoke
   Body: {"tool": "add", "args": {"a": 42, "b": 17}}
   ```
5. **Gateway validates and proxies**:
   - ✅ Server is approved → Forward to actual server
   - ❌ Server not in registry → Return 404
   - ❌ Server is suspended → Return 403

### What Developers See

When a server is **approved**:
- Server appears in Copilot's available tools
- Tools work normally

When a server is **pending/rejected/suspended**:
- Server does NOT appear in Copilot's tool list
- Cannot use that server's tools

---

## Enterprise Enforcement Configuration

### Option 1: GitHub Enterprise Managed Policy

Configure in GitHub Enterprise Cloud → Organization → Copilot → Policies:

```yaml
# Conceptual policy (configured via UI)
copilot:
  mcp:
    registry:
      url: "https://mcp-gateway.company.com/mcp"
    enforcement: "allowlist-only"
    allowLocalServers: false
```

### Option 2: VS Code Settings via MDM/GPO

For additional enforcement at the client level:

```json
{
  "github.copilot.chat.mcp.registry": "https://mcp-gateway.company.com/mcp",
  "github.copilot.chat.mcp.enforcementMode": "allowlist",
  "github.copilot.chat.mcp.allowLocalServers": false
}
```

Deploy via:
- **Windows**: Group Policy (ADMX templates)
- **macOS**: MDM profile (Jamf, Intune)
- **Linux**: Managed settings files

### Option 3: Network-Level Enforcement

Block direct MCP server connections:

```
# Allow only connections through the gateway
ALLOW: mcp-gateway.company.com:443
DENY: *.mcp-server.external.com:*
```

---

## Combining AD Groups with Copilot Registry

The full security model:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Security Control Points                               │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  1. Azure AD Authentication                                              │
│     ├── consec-example-read → Can use approved MCP servers               │
│     └── consec-example-admin → Can approve/deny MCP servers              │
│                                                                          │
│  2. MCP Gateway (Your Company)                                           │
│     ├── Only serves APPROVED servers to Copilot                          │
│     ├── Proxies all MCP traffic (audit trail)                            │
│     └── Blocks unapproved/suspended servers                              │
│                                                                          │
│  3. GitHub Copilot Enterprise Policy                                     │
│     ├── Registry URL → Points to your gateway                            │
│     └── Enforcement → Allowlist only                                     │
│                                                                          │
│  4. Client Configuration (MDM/GPO)                                       │
│     └── Forces VS Code to use company registry                           │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Verification Steps

### 1. Verify Gateway Returns Approved Servers Only

```bash
# Should return only approved servers
curl https://mcp-gateway.company.com/mcp/servers | jq '.servers[].status'
# Output: "Approved", "Approved", ...  (never Pending/Rejected)
```

### 2. Verify Copilot Uses the Registry

In VS Code:
1. Open Command Palette → `Developer: Show Logs`
2. Select `GitHub Copilot Chat`
3. Look for MCP registry fetch:
   ```
   [MCP] Fetching servers from registry: https://mcp-gateway.company.com/mcp/servers
   [MCP] Loaded 5 servers from registry
   ```

### 3. Test Server Access

```bash
# Approved server - should work
curl -X POST https://mcp-gateway.company.com/mcp/proxy/APPROVED-ID/invoke \
  -H "Content-Type: application/json" \
  -d '{"tool": "add", "args": {"a": 1, "b": 2}}'

# Non-existent server - should fail
curl -X POST https://mcp-gateway.company.com/mcp/proxy/NOT-EXISTS/invoke
# Returns: 404 Not Found
```

---

## Summary: Is Everything Configured?

| Component | Configured? | Action Needed |
|-----------|-------------|---------------|
| **AD Groups (consec-example-read/admin)** | ⚠️ Partial | Map groups to roles in Azure AD (see [ad-group-configuration.md](ad-group-configuration.md)) |
| **Gateway RBAC** | ✅ Yes | Uses `mcp.admin` role for admin actions |
| **Gateway Registry Endpoint** | ✅ Yes | `/mcp/servers` returns approved only |
| **Gateway Proxy Endpoint** | ✅ Yes | `/mcp/proxy/{id}` enforces approval |
| **Copilot Enterprise Policy** | ⚠️ Configure in GitHub | Set registry URL in org settings |
| **SSO/Azure AD** | ⚠️ Configure in Helm | Set tenantId/clientId in values-production.yaml |

---

## Configuration Checklist

### Gateway (Kubernetes)

```yaml
# values-production.yaml
auth:
  oidc:
    azureAd:
      tenantId: "YOUR_TENANT_ID"      # ← Configure
      clientId: "YOUR_CLIENT_ID"      # ← Configure
```

### Azure AD

1. [ ] Create `mcp.user` app role
2. [ ] Create `mcp.admin` app role  
3. [ ] Assign `consec-example-read` group → `mcp.user` role
4. [ ] Assign `consec-example-admin` group → `mcp.admin` role

### GitHub Enterprise

1. [ ] Go to Organization → Settings → Copilot → Policies
2. [ ] Set MCP Registry URL: `https://mcp-gateway.company.com/mcp`
3. [ ] Set Enforcement: `Allowlist only`

### MDM/GPO (Optional extra layer)

1. [ ] Deploy VS Code policy with registry URL
2. [ ] Block direct MCP connections at network level
