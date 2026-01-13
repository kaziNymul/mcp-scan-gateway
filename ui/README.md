# MCP Jurisdiction Admin UI

A Next.js web application for managing the MCP Server Registry, approvals, and audit logs.

## Features

- **SSO Authentication**: Azure AD / OIDC integration
- **Role-Based Access**:
  - **Users**: Register servers, upload local scans, view their servers
  - **Admins**: Approve/deny servers, view all servers, access audit logs, configure policies
- **Server Management**: Register, scan, and track MCP servers
- **Local Scan Upload**: For servers running on developer machines
- **Audit Logs**: View and export governance decisions
- **Policy Settings**: Configure risk thresholds and tool denylists

## Screenshots

### Server List
![Server List](docs/screenshots/server-list.png)

### Approval Workflow
![Approvals](docs/screenshots/approvals.png)

### Audit Logs
![Audit](docs/screenshots/audit.png)

## Prerequisites

- Node.js 18+ or 20+
- Azure AD application registration (for SSO)
- MCP Gateway API running

## Setup

### 1. Clone and Install

```bash
cd mcp-gateway/ui
npm install
```

### 2. Configure Environment

```bash
cp .env.example .env.local
```

Edit `.env.local`:

```env
# Azure AD Configuration
AZURE_AD_CLIENT_ID=your-app-client-id
AZURE_AD_CLIENT_SECRET=your-app-client-secret
AZURE_AD_TENANT_ID=your-tenant-id

# NextAuth.js
NEXTAUTH_URL=http://localhost:3000
NEXTAUTH_SECRET=generate-with-openssl-rand-base64-32

# MCP Gateway API
NEXT_PUBLIC_API_URL=http://localhost:8000
```

### 3. Configure Azure AD App Roles

In your Azure AD app registration, add these roles:

| Role | Value | Description |
|------|-------|-------------|
| Admin | `mcp.admin` | Full administrative access |
| User | `mcp.user` | Register and manage own servers |

See [docs/entra-app-roles.md](../docs/entra-app-roles.md) for detailed instructions.

### 4. Run Development Server

```bash
npm run dev
```

Open [http://localhost:3000](http://localhost:3000)

## Production Deployment

### Build

```bash
npm run build
```

### Docker

```dockerfile
FROM node:20-alpine AS builder
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM node:20-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production
COPY --from=builder /app/.next/standalone ./
COPY --from=builder /app/.next/static ./.next/static
COPY --from=builder /app/public ./public

EXPOSE 3000
ENV PORT=3000
CMD ["node", "server.js"]
```

### Kubernetes

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: mcp-jurisdiction-ui
spec:
  replicas: 2
  template:
    spec:
      containers:
        - name: ui
          image: your-registry/mcp-jurisdiction-ui:latest
          ports:
            - containerPort: 3000
          env:
            - name: AZURE_AD_CLIENT_ID
              valueFrom:
                secretKeyRef:
                  name: mcp-ui-secrets
                  key: azure-client-id
            - name: AZURE_AD_CLIENT_SECRET
              valueFrom:
                secretKeyRef:
                  name: mcp-ui-secrets
                  key: azure-client-secret
            - name: AZURE_AD_TENANT_ID
              valueFrom:
                secretKeyRef:
                  name: mcp-ui-secrets
                  key: azure-tenant-id
            - name: NEXTAUTH_URL
              value: https://mcp-jurisdiction.example.com
            - name: NEXTAUTH_SECRET
              valueFrom:
                secretKeyRef:
                  name: mcp-ui-secrets
                  key: nextauth-secret
            - name: NEXT_PUBLIC_API_URL
              value: http://mcp-jurisdiction-gateway:80
```

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Browser                               │
│  ┌─────────────────────────────────────────────────────┐│
│  │              Next.js UI (SSR + CSR)                 ││
│  │  ┌────────────┐ ┌────────────┐ ┌────────────────┐  ││
│  │  │  Servers   │ │ Approvals  │ │   Audit Logs   │  ││
│  │  └────────────┘ └────────────┘ └────────────────┘  ││
│  └─────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────┘
                           │
                           │ HTTPS (Bearer Token)
                           ▼
┌─────────────────────────────────────────────────────────┐
│                 MCP Gateway API                          │
│  ┌───────────────────────────────────────────────────┐  │
│  │  /registry/servers   /registry/audit   /mcp       │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────┐
│                     PostgreSQL                           │
│  ┌─────────────────┐ ┌───────────────┐ ┌─────────────┐  │
│  │ server_registry │ │  scan_results │ │ audit_events│  │
│  └─────────────────┘ └───────────────┘ └─────────────┘  │
└─────────────────────────────────────────────────────────┘
```

## Local Scan Upload Flow

For servers running on developer laptops:

1. **Register Server**: Select "Local (Developer Machine)" as source type
2. **Run MCP-Scan Locally**:
   ```bash
   pip install mcp-scan
   mcp-scan scan --config ~/.config/mcp/config.json --output-format json > scan.json
   ```
3. **Upload Results**: Paste the JSON output in the UI
4. **Request Approval**: Server moves to "Scanned" status, awaiting admin approval

## API Endpoints Used

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/registry/servers` | GET | List servers |
| `/registry/servers` | POST | Register server |
| `/registry/servers/{id}` | GET | Get server details |
| `/registry/servers/{id}/scan` | POST | Trigger remote scan |
| `/registry/servers/{id}/scan/upload` | POST | Upload local scan |
| `/registry/servers/{id}/approve` | POST | Approve server |
| `/registry/servers/{id}/deny` | POST | Deny server |
| `/registry/servers/{id}/suspend` | POST | Suspend server |
| `/registry/audit` | GET | Query audit events |
| `/registry/audit/stats` | GET | Get audit statistics |

## Development

```bash
# Run with hot reload
npm run dev

# Lint
npm run lint

# Type check
npx tsc --noEmit
```

## License

MIT License - see [LICENSE](../LICENSE)
