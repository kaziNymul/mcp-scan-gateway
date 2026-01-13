# MCP Jurisdiction - Enterprise MCP Server Governance

A complete enterprise solution for governing, scanning, and approving Model Context Protocol (MCP) servers before allowing them in GitHub Copilot and other AI assistants.

## 🎯 What This Does

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        MCP Jurisdiction Flow                                 │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   Developer                MCP Gateway              GitHub Copilot          │
│   ─────────               ───────────              ───────────────          │
│       │                        │                         │                   │
│       │ 1. Register MCP Server │                         │                   │
│       │───────────────────────>│                         │                   │
│       │                        │                         │                   │
│       │ 2. Run mcp-scan        │                         │                   │
│       │───────────────────────>│                         │                   │
│       │                        │                         │                   │
│       │ 3. Upload scan results │                         │                   │
│       │───────────────────────>│                         │                   │
│       │                        │                         │                   │
│       │            ┌───────────┴───────────┐             │                   │
│       │            │ Security Team Reviews │             │                   │
│       │            │ (or auto-approve if   │             │                   │
│       │            │  risk score < 25)     │             │                   │
│       │            └───────────┬───────────┘             │                   │
│       │                        │                         │                   │
│       │                        │ 4. Fetch allowed list   │                   │
│       │                        │<────────────────────────│                   │
│       │                        │                         │                   │
│       │                        │ 5. Only approved servers│                   │
│       │                        │────────────────────────>│                   │
│       │                        │                         │                   │
└─────────────────────────────────────────────────────────────────────────────┘
```

## 📦 Components

| Component | Description | Location |
|-----------|-------------|----------|
| **MCP Gateway** | .NET Core API for registry, scanning, policy | `dotnet/` |
| **mcp-scan** | Security scanner for MCP servers | `../mcp-scan/` (build from source) |
| **Admin UI** | Next.js dashboard for approvals | `ui/` |
| **Static UI** | Lightweight HTML dashboard | `ui-static/` |
| **Helm Charts** | Kubernetes deployment | `deploy/helm/` |
| **CLI Tool** | Developer command-line interface | `scripts/mcp-jurisdiction` |

## 🚀 Quick Start (Local Testing)

### Prerequisites
- Docker & Docker Compose
- Python 3.10+
- Node.js 18+ (for UI)

### 1. Start the Gateway

```bash
docker-compose -f docker-compose.test.yml up -d
```

### 2. Build and Install mcp-scan

```bash
cd ../mcp-scan
python3 -m build
pip install dist/mcp_scan-*.whl
```

### 3. Scan an MCP Server

```bash
# Create test config
cat > /tmp/test-config.json << 'EOF'
{
  "mcpServers": {
    "math-server": {
      "command": "python3",
      "args": ["path/to/your/mcp_server.py"]
    }
  }
}
EOF

# Scan it
mcp-scan inspect /tmp/test-config.json
mcp-scan scan /tmp/test-config.json --json > scan-results.json
```

### 4. Register and Upload

```bash
# Register the server
curl -X POST http://localhost:8080/registry/servers \
  -H "Content-Type: application/json" \
  -d '{
    "name": "My MCP Server",
    "canonicalId": "python:my_server.py",
    "serverUrl": "stdio://local",
    "ownerTeam": "my-team"
  }'

# Upload scan results
curl -X POST "http://localhost:8080/scan/upload?server_id=<SERVER_ID>" \
  -H "Content-Type: application/json" \
  -d @scan-results.json
```

### 5. View Dashboard

Open http://localhost:8080 in your browser.

---

## 🏢 Enterprise Deployment

### Complete Configuration Checklist

See [docs/production-configuration.md](docs/production-configuration.md) for the full guide.

### Summary of Required Changes

#### 1. Azure AD Configuration

| Task | Where |
|------|-------|
| Create App Registration | Azure Portal → Entra ID |
| Create `mcp.user` app role | App Registration → App roles |
| Create `mcp.admin` app role | App Registration → App roles |
| Assign AD groups to roles | Enterprise App → Users and groups |

See [docs/ad-group-configuration.md](docs/ad-group-configuration.md) for step-by-step instructions.

#### 2. Kubernetes Secrets

```bash
kubectl create namespace mcp-jurisdiction

kubectl create secret generic mcp-postgres-secret \
  -n mcp-jurisdiction \
  --from-literal=password='YOUR_DB_PASSWORD'

kubectl create secret generic mcp-oidc-secret \
  -n mcp-jurisdiction \
  --from-literal=client-secret='YOUR_AZURE_AD_CLIENT_SECRET'
```

#### 3. Helm Values

Create `deploy/helm/mcp-jurisdiction/values-production.yaml`:

```yaml
ingress:
  hosts:
    - host: mcp-gateway.yourcompany.com  # ← CHANGE

auth:
  oidc:
    azureAd:
      tenantId: "YOUR_TENANT_ID"         # ← CHANGE
      clientId: "YOUR_CLIENT_ID"         # ← CHANGE

externalPostgres:
  host: "postgres.yourcompany.com"       # ← CHANGE
```

#### 4. Deploy

```bash
helm upgrade --install mcp-jurisdiction ./deploy/helm/mcp-jurisdiction \
  -f ./deploy/helm/mcp-jurisdiction/values-production.yaml \
  -n mcp-jurisdiction
```

#### 5. Artifactory Setup

```bash
# Build mcp-scan
cd ../mcp-scan && python3 -m build

# Upload to Artifactory
twine upload \
  --repository-url https://artifactory.yourcompany.com/api/pypi/python-repo \
  dist/mcp_scan-*.whl
```

#### 6. GitHub Copilot Enterprise

Configure in GitHub Organization → Settings → Copilot → Policies:

| Setting | Value |
|---------|-------|
| MCP Registry URL | `https://mcp-gateway.yourcompany.com/mcp` |
| Enforcement | `Allowlist only` |

See [docs/copilot-registry-configuration.md](docs/copilot-registry-configuration.md) for details.

---

## 📋 All Configuration Points

| Component | File | What to Change |
|-----------|------|----------------|
| **Helm Values** | `deploy/helm/mcp-jurisdiction/values-production.yaml` | Gateway URL, SSO, Database |
| **Developer Setup** | `scripts/setup-mcp-scan.sh` lines 24-27 | Artifactory URL, Gateway URL |
| **CLI Tool** | `scripts/mcp-jurisdiction` line 29 | Gateway URL |
| **Static UI** | `ui-static/index.html` line 286 | API URL |
| **Next.js UI** | `ui/.env.local` | Azure AD credentials |

---

## 🔐 Access Control

| AD Group | App Role | Permissions |
|----------|----------|-------------|
| `consec-example-read` | `mcp.user` | Register, scan, view dashboard |
| `consec-example-admin` | `mcp.admin` | Approve, deny, suspend servers |

---

## 📚 Documentation

| Document | Description |
|----------|-------------|
| [Production Configuration](docs/production-configuration.md) | Complete deployment guide |
| [AD Group Configuration](docs/ad-group-configuration.md) | Azure AD setup |
| [Copilot Integration](docs/copilot-integration.md) | GitHub Copilot setup |
| [Copilot Registry](docs/copilot-registry-configuration.md) | Enterprise policy |
| [Artifactory Distribution](docs/artifactory-distribution.md) | Package hosting |
| [Local Scanning Guide](docs/local-scanning-guide.md) | Developer workflow |
| [Security](docs/security.md) | Security considerations |

---

## 🧪 Testing

### Run Unit Tests

```bash
cd dotnet
dotnet test
```

### Run E2E Tests

```bash
./test-e2e.sh
```

---

## 📝 License

Based on Microsoft MCP Gateway. See [LICENSE](LICENSE) for details.

---

## 🆘 Support

For issues or questions:
1. Check the [documentation](docs/)
2. Open a GitHub issue
3. Contact your security team for approval requests
