# MCP Gateway Repository Analysis

## Overview

This document provides a technical analysis of the Microsoft MCP Gateway repository structure and identifies integration points for the MCP Jurisdiction + Registry Approval + Security Scanning solution.

**Analysis Date:** January 12, 2026  
**Analyst:** Platform Security Engineering Team

---

## 1. Tech Stack

### Primary Language & Framework
- **Language:** C# (.NET 8.0)
- **Framework:** ASP.NET Core
- **Platform:** x64

### Key Dependencies
| Package | Version | Purpose |
|---------|---------|---------|
| KubernetesClient | 18.0.5 | K8s API interaction for deployment management |
| Microsoft.Azure.Cosmos | 3.51.0 | Production metadata storage |
| Microsoft.Extensions.Caching.StackExchangeRedis | 9.0.10 | Development session/cache storage |
| Microsoft.Identity.Web | 3.9.1 | Azure AD authentication |
| ModelContextProtocol | 0.4.0-preview.3 | MCP protocol implementation |
| ModelContextProtocol.AspNetCore | 0.4.0-preview.3 | ASP.NET Core MCP integration |

### Build System
- **Solution File:** `dotnet/Microsoft.McpGateway.sln`
- **Central Package Management:** `Directory.Packages.props`
- **Projects:**
  1. `Microsoft.McpGateway.Service` - Main gateway service
  2. `Microsoft.McpGateway.Management` - Management/control plane library
  3. `Microsoft.McpGateway.Tools` - Tool gateway router

---

## 2. Key Folders & Files

```
mcp-gateway/
├── dotnet/
│   ├── Microsoft.McpGateway.Service/         # Main gateway service
│   │   └── src/
│   │       ├── Program.cs                    # Application entrypoint
│   │       ├── Controllers/
│   │       │   ├── ManagementController.cs   # Adapter CRUD (/adapters)
│   │       │   ├── ToolManagementController.cs # Tool CRUD (/tools)
│   │       │   ├── AdapterReverseProxyController.cs # Request proxying
│   │       │   └── PingController.cs         # Health check
│   │       ├── Authentication/
│   │       │   └── DevelopmentAuthenticationHandler.cs
│   │       ├── Routing/                      # Session routing logic
│   │       └── Session/                      # Session management
│   │
│   ├── Microsoft.McpGateway.Management/      # Core management library
│   │   └── src/
│   │       ├── Authorization/
│   │       │   ├── IPermissionProvider.cs    # Permission interface
│   │       │   └── SimplePermissionProvider.cs # Role-based permissions
│   │       ├── Contracts/
│   │       │   ├── AdapterResource.cs        # Adapter data model
│   │       │   ├── ToolResource.cs           # Tool data model
│   │       │   └── IManagedResource.cs       # Base resource interface
│   │       ├── Deployment/
│   │       │   └── KubernetesAdapterDeploymentManager.cs
│   │       ├── Service/
│   │       │   ├── AdapterManagementService.cs
│   │       │   └── ToolManagementService.cs
│   │       └── Store/
│   │           ├── IAdapterResourceStore.cs  # Storage interface
│   │           ├── CosmosAdapterResourceStore.cs
│   │           ├── RedisAdapterResourceStore.cs
│   │           └── InMemoryAdapterResourceStore.cs
│   │
│   └── Microsoft.McpGateway.Tools/           # Tool gateway router
│
├── deployment/
│   ├── k8s/
│   │   ├── local-deployment.yml              # Local K8s manifests
│   │   └── cloud-deployment-template.yml     # Cloud template
│   └── infra/
│       └── azure-deployment.bicep            # Azure IaC
│
├── openapi/
│   └── mcp-gateway.openapi.json              # API specification
│
└── sample-servers/                           # Example MCP servers
```

---

## 3. Architecture Patterns

### 3.1 Authentication
- **Development Mode:** `DevelopmentAuthenticationHandler` - bypasses real auth
- **Production Mode:** Azure AD + JWT Bearer tokens via `Microsoft.Identity.Web`
- **MCP-specific:** `McpSubPathAwareAuthenticationHandler` for MCP authentication

### 3.2 Authorization
- **Interface:** `IPermissionProvider`
- **Implementation:** `SimplePermissionProvider`
- **Roles:** `mcp.admin` role for elevated access
- **Operations:** `Read` and `Write` operations checked per resource
- **Owner-based:** Resource creators have full access

### 3.3 Storage
- **Interface-based:** `IAdapterResourceStore`, `IToolResourceStore`
- **Implementations:**
  - `CosmosAdapterResourceStore` (Production)
  - `RedisAdapterResourceStore` (Development)
  - `InMemoryAdapterResourceStore` (Testing)

### 3.4 Request Flow
```
Client Request
    ↓
Authentication Middleware (JWT validation)
    ↓
Authorization ([Authorize] attribute)
    ↓
Controller (ManagementController / AdapterReverseProxyController)
    ↓
Service Layer (AdapterManagementService)
    ↓
Permission Check (IPermissionProvider)
    ↓
Storage (IAdapterResourceStore)
    ↓
Kubernetes (IKubeClientWrapper)
```

---

## 4. Integration Points for MCP Jurisdiction

### 4.1 Policy Enforcement Hooks

| Hook Location | File Path | Purpose |
|---------------|-----------|---------|
| **Pre-proxy check** | `Controllers/AdapterReverseProxyController.cs` → `EnsureAdapterReadAccessAsync()` | Add registry status + allowlist check |
| **Permission provider** | `Authorization/SimplePermissionProvider.cs` | Extend for team-based + tool-level restrictions |
| **Request pipeline** | `Program.cs` → middleware chain | Add audit logging + policy middleware |
| **New controller** | `Controllers/RegistryController.cs` (NEW) | Registry API endpoints |
| **New controller** | `Controllers/ScannerController.cs` (NEW) | Scanner trigger endpoints |
| **New controller** | `Controllers/AuditController.cs` (NEW) | Audit log endpoints |

### 4.2 Storage Extensions

**New PostgreSQL Storage (to be added):**
```
Store/
├── Postgres/
│   ├── PostgresConnectionFactory.cs
│   ├── PostgresServerRegistryStore.cs
│   ├── PostgresScanResultStore.cs
│   ├── PostgresApprovalStore.cs
│   └── PostgresAuditEventStore.cs
```

### 4.3 New Services

**Registry Service (to be added):**
```
Service/
├── Registry/
│   ├── IServerRegistryService.cs
│   ├── ServerRegistryService.cs
│   ├── IScannerService.cs
│   ├── ScannerService.cs
│   ├── IApprovalService.cs
│   └── ApprovalService.cs
```

### 4.4 New Contracts/Models

**Registry Domain Models (to be added):**
```
Contracts/
├── Registry/
│   ├── ServerRegistration.cs
│   ├── ServerStatus.cs (enum)
│   ├── ScanResult.cs
│   ├── ScanStatus.cs (enum)
│   ├── Approval.cs
│   ├── AuditEvent.cs
│   └── PolicyConfig.cs
```

### 4.5 Policy Enforcement Middleware

**New Middleware (to be added):**
```
Middleware/
├── JurisdictionEnforcementMiddleware.cs
├── AuditLoggingMiddleware.cs
└── RateLimitingMiddleware.cs
```

---

## 5. MCP-Scan Integration Points

### 5.1 Scanner Repository Analysis (Invariant Labs)

**Tech Stack:** Python 3.10+, FastAPI, Pydantic

**Key Components:**
- `MCPScanner` class - Core scanning logic
- `cli.py` - Command-line interface
- `models.py` - Scan result models
- `verify_api.py` - Remote analysis API

**Scan Capabilities:**
1. Parse MCP configuration files (Claude, VSCode formats)
2. Connect to MCP servers and introspect tools
3. Detect entity changes (tools, prompts, resources)
4. Call remote analysis API for risk scoring

### 5.2 Integration Strategy

**Kubernetes Job Pattern:**
```yaml
# Scanner runs as a K8s Job
apiVersion: batch/v1
kind: Job
metadata:
  name: mcp-scan-{server-id}
spec:
  template:
    spec:
      containers:
      - name: mcp-scan
        image: mcp-scan:latest
        command: ["mcp-scan", "scan", "--json", "/config/mcp-config.json"]
```

**Integration Flow:**
```
Registry Service
    ↓ (creates Job)
Kubernetes API
    ↓ (schedules)
MCP-Scan Job Pod
    ↓ (outputs JSON)
Scan Report → PostgreSQL
    ↓
Webhook/Polling → Registry Service
```

---

## 6. Configuration System

### 6.1 Current Configuration
- **appsettings.json** - Base configuration
- **appsettings.Development.json** - Dev overrides
- Environment variables for secrets

### 6.2 New Configuration Sections (to be added)
```json
{
  "Jurisdiction": {
    "Enabled": true,
    "EnforcementMode": "Strict",
    "BypassRisk": "Documented"
  },
  "Registry": {
    "PostgresConnection": "Host=...",
    "AutoApprove": false
  },
  "Scanner": {
    "Image": "ghcr.io/invariantlabs-ai/mcp-scan:latest",
    "Timeout": "300s",
    "RiskThreshold": 0.7
  },
  "Policy": {
    "GlobalDenylist": ["filesystem_write", "network_arbitrary"],
    "DefaultTimeoutMs": 30000,
    "MaxPayloadBytes": 1048576
  }
}
```

---

## 7. Observability

### 7.1 Current
- Application Insights telemetry
- Console logging (Development)

### 7.2 Extensions Needed
- Prometheus `/metrics` endpoint
- Structured JSON logging
- Audit event stream
- OpenTelemetry traces (optional)

---

## 8. Deployment Model

### 8.1 Current
- Kubernetes StatefulSet for gateway
- Redis for development caching
- CosmosDB for production storage
- Container images via local registry or ACR

### 8.2 Extensions for MKE
- Helm chart packaging
- PostgreSQL StatefulSet or external
- Scanner Job RBAC
- NetworkPolicies
- Ingress with TLS

---

## 9. Security Considerations

### 9.1 Bypass Risk (CRITICAL)
**This solution CANNOT:**
- Scan laptops or detect unregistered MCP servers
- Block direct local connections from clients to servers
- Enforce anything outside the gateway traffic path

**This solution CAN:**
- Scan declared/submitted server configurations
- Audit traffic that passes through the gateway
- Enforce allowlists for gateway-routed requests
- Provide visibility into registered servers' risk profiles

### 9.2 Compensating Controls
1. Copilot/Client registry allowlist enforcement
2. MDM/endpoint policy (out of scope but recommended)
3. Running high-risk servers remotely in cluster
4. User education on governance requirements

---

## 10. Implementation Priority

### Phase 1: Registry + Storage
1. PostgreSQL storage layer
2. Server registration API
3. Status state machine

### Phase 2: Scanner Integration
1. K8s Job scheduler
2. MCP-Scan container
3. Result ingestion

### Phase 3: Policy Enforcement
1. Jurisdiction middleware
2. Allowlist/denylist checks
3. Audit logging

### Phase 4: Observability + Deployment
1. Prometheus metrics
2. Helm chart
3. Documentation

---

## Appendix: File Modification Summary

### Files to Modify
| File | Changes |
|------|---------|
| `Program.cs` | Add DI for registry, scanner, policy services; add middleware |
| `Directory.Packages.props` | Add Npgsql, Prometheus packages |
| `Microsoft.McpGateway.Management.csproj` | Add Npgsql reference |
| `Microsoft.McpGateway.Service.csproj` | Add new package references |

### Files to Create
| File | Purpose |
|------|---------|
| `Controllers/RegistryController.cs` | Registry API |
| `Controllers/AuditController.cs` | Audit API |
| `Store/Postgres/*.cs` | PostgreSQL storage |
| `Service/Registry/*.cs` | Registry services |
| `Contracts/Registry/*.cs` | Domain models |
| `Middleware/*.cs` | Policy enforcement |
| `Scanner/*.cs` | K8s job management |

---

*This analysis forms the basis for the MCP Jurisdiction implementation. All changes will follow the existing patterns and conventions observed in the Microsoft MCP Gateway codebase.*
