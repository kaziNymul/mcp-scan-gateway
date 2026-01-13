# MCP Jurisdiction - Production Configuration Guide

This guide documents **all configuration points** you need to update when deploying MCP Jurisdiction to production (MKE cluster with SSO and Artifactory).

## Configuration Summary

| Component | File(s) | Configuration Type |
|-----------|---------|-------------------|
| **Gateway URL** | Helm values, scripts, UI | Kubernetes + Client-side |
| **Artifactory URL** | Setup script, docs | Client-side |
| **SSO/OIDC** | Helm values, secrets | Kubernetes |
| **Database** | Helm values, secrets | Kubernetes |

---

## 1. Kubernetes Deployment (Helm)

### Primary Configuration File
📁 `deploy/helm/mcp-jurisdiction/values.yaml`

Create a production values override file:

```bash
# deploy/helm/mcp-jurisdiction/values-production.yaml
```

```yaml
# ============================================================================
# Gateway URL Configuration
# ============================================================================
ingress:
  enabled: true
  className: nginx  # or your MKE ingress class (e.g., traefik, contour)
  annotations:
    nginx.ingress.kubernetes.io/proxy-body-size: "10m"
    nginx.ingress.kubernetes.io/proxy-read-timeout: "300"
    # Add SSO annotations if using ingress-level auth
    # nginx.ingress.kubernetes.io/auth-url: "https://auth.yourcompany.com/verify"
  hosts:
    - host: mcp-gateway.yourcompany.com  # <-- CHANGE THIS
      paths:
        - path: /
          pathType: Prefix
  tls:
    - secretName: mcp-gateway-tls
      hosts:
        - mcp-gateway.yourcompany.com  # <-- CHANGE THIS

# ============================================================================
# SSO / OIDC Configuration  
# ============================================================================
auth:
  development:
    enabled: false  # Disable dev mode in production
  
  oidc:
    enabled: true
    # For Okta:
    issuerUrl: "https://yourcompany.okta.com"
    audience: "api://mcp-gateway"
    
    # For Azure AD:
    azureAd:
      tenantId: "your-tenant-id"        # <-- CHANGE THIS
      clientId: "your-app-client-id"    # <-- CHANGE THIS
    
    # For Keycloak:
    # issuerUrl: "https://keycloak.yourcompany.com/realms/mcp"
    # audience: "mcp-gateway-client"

# ============================================================================
# Database Configuration
# ============================================================================
postgresql:
  enabled: false  # Use external database in production

externalPostgres:
  host: "postgres.yourcompany.com"      # <-- CHANGE THIS
  port: 5432
  database: mcpgov
  username: mcpgov
  existingSecret: "mcp-postgres-secret"  # Reference K8s secret
  existingSecretPasswordKey: password

# ============================================================================
# Production Settings
# ============================================================================
gateway:
  replicaCount: 3  # Scale for HA
  aspnetcoreEnvironment: Production

jurisdiction:
  enforcementMode: Enforce
  policy:
    riskThreshold: 0.7
    requireAdminForHighRisk: true
    enforceRegistryOnly: true
```

### Deploy with Custom Values

```bash
helm upgrade --install mcp-jurisdiction ./deploy/helm/mcp-jurisdiction \
  -f ./deploy/helm/mcp-jurisdiction/values-production.yaml \
  -n mcp-jurisdiction \
  --create-namespace
```

---

## 2. Secrets Configuration

📁 `deploy/manifests/secrets.yaml` (or use external secrets manager)

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: mcp-postgres-secret
  namespace: mcp-jurisdiction
type: Opaque
stringData:
  password: "YOUR_POSTGRES_PASSWORD"  # <-- CHANGE THIS

---
apiVersion: v1
kind: Secret
metadata:
  name: mcp-oidc-secret
  namespace: mcp-jurisdiction
type: Opaque
stringData:
  client-secret: "YOUR_OIDC_CLIENT_SECRET"  # <-- CHANGE THIS (if using confidential client)
```

---

## 3. Developer Setup Script (Artifactory)

📁 `scripts/setup-mcp-scan.sh` - **Lines 24-27**

```bash
# ============================================================================
# CONFIGURATION - UPDATE THESE VALUES FOR YOUR ENVIRONMENT
# ============================================================================
ARTIFACTORY_URL="${MCP_ARTIFACTORY_URL:-https://artifactory.yourcompany.com}"  # <-- CHANGE
PYPI_REPO_PATH="api/pypi/python-repo/simple"                                     # <-- CHANGE if different
GATEWAY_URL="${MCP_GATEWAY_URL:-https://mcp-gateway.yourcompany.com}"            # <-- CHANGE
SCRIPTS_URL="https://artifactory.yourcompany.com/artifactory/generic-repo/mcp-tools"  # <-- CHANGE
```

After updating, upload to Artifactory:
```bash
curl -u admin:password -T scripts/setup-mcp-scan.sh \
  "https://artifactory.yourcompany.com/artifactory/generic-repo/mcp-tools/"
```

---

## 4. Admin UI (Static)

📁 `ui-static/index.html` - **Line 286**

```javascript
// API Configuration - UPDATE FOR PRODUCTION
const API_URL = 'https://mcp-gateway.yourcompany.com';  // <-- CHANGE THIS
```

For production, consider making this configurable:

```javascript
// Load from environment or meta tag
const API_URL = document.querySelector('meta[name="api-url"]')?.content 
  || window.MCP_CONFIG?.apiUrl 
  || 'https://mcp-gateway.yourcompany.com';
```

---

## 5. CLI Tool

📁 `scripts/mcp-jurisdiction` - **Line 29**

```python
DEFAULT_GATEWAY = "https://mcp-gateway.yourcompany.com"  # <-- CHANGE THIS
```

Or users can set environment variable:
```bash
export MCP_GATEWAY_URL="https://mcp-gateway.yourcompany.com"
```

---

## 6. Documentation Updates

Update all documentation files with your actual URLs:

### Files to Update:

| File | What to Change |
|------|---------------|
| `docs/artifactory-distribution.md` | Artifactory URL, Gateway URL, PyPI path |
| `docs/local-scanning-guide.md` | Artifactory PyPI URL |
| `docs/copilot-integration.md` | Gateway URL for Copilot configuration |
| `examples/local-server/README.md` | GATEWAY_URL examples |
| `examples/container-server/README.md` | GATEWAY_URL examples |

Quick sed replacement:
```bash
# Replace placeholder URLs in all docs
find docs examples -name "*.md" -exec sed -i \
  -e 's|mcp-gateway.yourcompany.com|mcp-gateway.acme.com|g' \
  -e 's|artifactory.yourcompany.com|artifactory.acme.com|g' \
  -e 's|yourcompany.okta.com|acme.okta.com|g' \
  {} \;
```

---

## 7. GitHub Copilot Enterprise Policy

For VS Code/Copilot to enforce the gateway, configure enterprise policies:

### Option A: MDM/Group Policy

📁 Deploy via Intune/SCCM/Jamf:

```json
{
  "github.copilot.chat.mcp.proxy": "https://mcp-gateway.yourcompany.com/mcp/proxy",
  "github.copilot.chat.mcp.allowedServers": [
    "https://mcp-gateway.yourcompany.com/mcp/proxy/*"
  ]
}
```

### Option B: settings.json (user-level)

```json
{
  "github.copilot.chat.mcp.proxy": "https://mcp-gateway.yourcompany.com/mcp/proxy"
}
```

---

## Complete Configuration Checklist

### Before Deployment

- [ ] **Helm values-production.yaml**
  - [ ] `ingress.hosts[0].host` → Your gateway FQDN
  - [ ] `ingress.tls[0].hosts[0]` → Your gateway FQDN  
  - [ ] `auth.oidc.issuerUrl` → Your SSO provider URL
  - [ ] `auth.oidc.azureAd.tenantId` → Azure AD tenant (if using)
  - [ ] `auth.oidc.azureAd.clientId` → App registration client ID
  - [ ] `externalPostgres.host` → Production database host

- [ ] **Secrets**
  - [ ] PostgreSQL password in Kubernetes secret
  - [ ] OIDC client secret (if confidential client)
  - [ ] TLS certificate for ingress

- [ ] **scripts/setup-mcp-scan.sh**
  - [ ] `ARTIFACTORY_URL` → Your Artifactory URL
  - [ ] `PYPI_REPO_PATH` → Your PyPI repo path in Artifactory
  - [ ] `GATEWAY_URL` → Your gateway URL

- [ ] **ui-static/index.html**
  - [ ] `API_URL` → Your gateway URL

- [ ] **scripts/mcp-jurisdiction**
  - [ ] `DEFAULT_GATEWAY` → Your gateway URL

- [ ] **Artifactory**
  - [ ] mcp-scan package uploaded to PyPI repo
  - [ ] setup-mcp-scan.sh uploaded to generic repo

- [ ] **Enterprise Policy**
  - [ ] Copilot MCP proxy URL configured in MDM

---

## Quick Reference: All URLs in One Place

```bash
# Production URLs - set these in your environment
export MCP_GATEWAY_URL="https://mcp-gateway.yourcompany.com"
export MCP_ARTIFACTORY_URL="https://artifactory.yourcompany.com"
export MCP_PYPI_PATH="api/pypi/python-repo/simple"
export MCP_SSO_ISSUER="https://yourcompany.okta.com"  # or Azure AD URL
```

---

## SSO Provider-Specific Configuration

### Azure AD / Entra ID

1. Create App Registration in Azure Portal
2. Configure:
   - **Redirect URI**: `https://mcp-gateway.yourcompany.com/auth/callback`
   - **API Permissions**: `User.Read`, `openid`, `profile`, `email`
   - **App Roles**: Create `Admin`, `Developer`, `Viewer` roles

Helm values:
```yaml
auth:
  oidc:
    azureAd:
      tenantId: "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
      clientId: "yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy"
```

### Okta

1. Create OIDC App in Okta Admin
2. Configure:
   - **Sign-in redirect URI**: `https://mcp-gateway.yourcompany.com/auth/callback`
   - **Allowed grant types**: Authorization Code + PKCE

Helm values:
```yaml
auth:
  oidc:
    issuerUrl: "https://yourcompany.okta.com"
    audience: "api://mcp-gateway"
```

### Keycloak

Helm values:
```yaml
auth:
  oidc:
    issuerUrl: "https://keycloak.yourcompany.com/realms/mcp"
    audience: "mcp-gateway-client"
```

---

## Validation Commands

After deployment, verify configuration:

```bash
# Check gateway is accessible
curl -I https://mcp-gateway.yourcompany.com/health

# Check SSO is configured (should redirect or return 401)
curl -I https://mcp-gateway.yourcompany.com/registry/servers

# Check Artifactory has mcp-scan
pip index versions mcp-scan \
  --index-url https://artifactory.yourcompany.com/api/pypi/python-repo/simple

# Test developer setup script
curl -sSL https://artifactory.yourcompany.com/artifactory/generic-repo/mcp-tools/setup-mcp-scan.sh | bash
```
