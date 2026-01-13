# Policy Configuration Examples

This directory contains example policy configurations for the MCP Jurisdiction system.

## Policy Configuration Overview

The policy configuration controls:
- **Tool Denylists**: Block specific tools globally or per-team
- **Team Allowlists**: Restrict server access by team
- **Risk Thresholds**: Define acceptable scan risk scores
- **Rate Limits**: Prevent abuse with per-user/team limits

## Example: Restrictive Security Policy

```json
{
  "globalToolDenylist": [
    "shell_execute",
    "arbitrary_code_run",
    "file_delete",
    "network_request_raw"
  ],
  "deniedToolCategories": [
    "system_admin",
    "network_raw",
    "code_execution"
  ],
  "teamAllowlists": {
    "security-team": ["*"],
    "platform-team": [
      "github.com/modelcontextprotocol/*",
      "internal/platform-*"
    ],
    "development-team": [
      "internal/dev-*"
    ]
  },
  "teamDenylists": {
    "contractor-team": [
      "internal/finance-*",
      "internal/hr-*"
    ]
  },
  "rateLimitPerUser": 100,
  "rateLimitPerTeam": 1000,
  "defaultTimeoutMs": 30000,
  "maxRequestPayloadBytes": 1048576,
  "maxResponsePayloadBytes": 10485760,
  "riskThreshold": 50,
  "scanPassThreshold": 30,
  "requireAdminForHighRisk": true,
  "enforceRegistryOnly": true,
  "bypassAllowedPrincipals": [
    "admin@company.com",
    "service-account-emergency@company.iam.gserviceaccount.com"
  ]
}
```

## Example: Development/Permissive Policy

```json
{
  "globalToolDenylist": [],
  "deniedToolCategories": [],
  "teamAllowlists": {},
  "teamDenylists": {},
  "rateLimitPerUser": 10000,
  "rateLimitPerTeam": 100000,
  "defaultTimeoutMs": 60000,
  "maxRequestPayloadBytes": 52428800,
  "maxResponsePayloadBytes": 104857600,
  "riskThreshold": 90,
  "scanPassThreshold": 70,
  "requireAdminForHighRisk": false,
  "enforceRegistryOnly": false,
  "bypassAllowedPrincipals": ["*"]
}
```

## Policy Enforcement Modes

| Mode | Behavior |
|------|----------|
| `Enforce` | Block non-compliant requests |
| `AuditOnly` | Log violations but allow requests |
| `Disabled` | No policy checks (not recommended) |

## Applying Policy Changes

### Via Helm Values

```yaml
# values.yaml
jurisdiction:
  policy:
    globalToolDenylist:
      - "shell_execute"
    riskThreshold: 50
```

```bash
helm upgrade mcp-jurisdiction ./mcp-jurisdiction -f values.yaml
```

### Via ConfigMap Update

```bash
kubectl edit configmap mcp-jurisdiction-config -n mcp-jurisdiction
# Pods will need restart to pick up changes
kubectl rollout restart deployment/mcp-jurisdiction-gateway -n mcp-jurisdiction
```

### Via API (Coming Soon)

```bash
curl -X PUT "${GATEWAY_URL}/registry/policy" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d @policy.json
```

## Policy Best Practices

1. **Start with AuditOnly mode** to understand traffic patterns
2. **Review audit logs** before enabling Enforce mode
3. **Use team allowlists** for least-privilege access
4. **Set conservative risk thresholds** (50 or lower)
5. **Require admin approval** for high-risk servers
6. **Minimize bypass principals** to emergency accounts only
