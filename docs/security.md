# MCP Jurisdiction Security Documentation

## Executive Summary

MCP Jurisdiction provides a **governance boundary** for Model Context Protocol (MCP) servers within an organization. It enforces policy through a centralized gateway, requiring all MCP servers to be:

1. **Registered** in the central registry
2. **Scanned** for security vulnerabilities
3. **Approved** by authorized personnel
4. **Monitored** through comprehensive audit logging

---

## ⚠️ Critical Scope Limitations

### What This Solution CAN Do

✅ Enforce policy on MCP traffic proxied through the gateway  
✅ Require registration before a server can be accessed  
✅ Scan registered servers for known vulnerabilities  
✅ Block access to unregistered/unapproved servers  
✅ Audit all tool calls through the gateway  
✅ Apply team-based access controls  

### What This Solution CANNOT Do

❌ **Detect MCP servers on developer laptops** - Local servers bypass the gateway entirely  
❌ **Scan servers that aren't registered** - No automatic discovery  
❌ **Block direct MCP connections** - Only gateway-proxied traffic is controlled  
❌ **Prevent clipboard/copy attacks** - Out of scope  
❌ **Guarantee zero false negatives** - Scanning is not 100% effective  

### The Bypass Risk

**Any user with direct network access to an MCP server can bypass this governance system.**

Mitigations:
1. Network segmentation to isolate MCP servers behind the gateway
2. Firewall rules blocking direct access to MCP server ports
3. EDR/endpoint policies preventing local MCP server execution
4. Security awareness training for developers

---

## Threat Model

### Assets

| Asset | Sensitivity | Location |
|-------|-------------|----------|
| Registry Database | High | PostgreSQL |
| Audit Logs | High | PostgreSQL |
| Authentication Tokens | Critical | Azure AD / JWT |
| MCP Server Credentials | High | K8s Secrets |
| Policy Configuration | Medium | ConfigMap |

### Threat Actors

| Actor | Motivation | Capability |
|-------|------------|------------|
| External Attacker | Data exfiltration, service disruption | Medium-High |
| Malicious Insider | Unauthorized access, policy bypass | High |
| Compromised MCP Server | Lateral movement, data theft | Medium |
| Supply Chain Attacker | Backdoor installation | High |

### Attack Vectors

#### 1. Unregistered Server Bypass
**Risk**: User connects directly to MCP server without gateway
**Mitigation**: Network policies, firewall rules, endpoint controls

#### 2. Scanner Evasion
**Risk**: Malicious server passes scan, then changes behavior
**Mitigation**: Continuous scanning, runtime monitoring, signed attestations

#### 3. Token Theft
**Risk**: Stolen JWT grants unauthorized registry access
**Mitigation**: Short token lifetimes, Azure AD conditional access, IP restrictions

#### 4. Privilege Escalation
**Risk**: Non-admin approves high-risk server
**Mitigation**: Role-based access control, audit logging, alerts

#### 5. Supply Chain Attack
**Risk**: Compromised dependency in MCP server
**Mitigation**: Dependency scanning, SBOM generation, pinned versions

#### 6. Prompt Injection via Tool
**Risk**: MCP tool returns malicious content
**Mitigation**: Output sanitization, content filtering, Invariant Guardrails

---

## Security Controls

### Authentication & Authorization

| Control | Implementation | Status |
|---------|---------------|--------|
| User Authentication | Azure AD / OIDC | ✅ |
| Service-to-Service | JWT with roles | ✅ |
| Role-Based Access | Admin, Approver, User | ✅ |
| API Key Support | SHA-256 hashed | ⏳ Planned |

**Required Roles:**
- `mcp.registry.read` - View servers and scan results
- `mcp.registry.write` - Register and modify servers
- `mcp.registry.approve` - Approve/deny servers
- `mcp.registry.admin` - Full administrative access

### Data Protection

| Control | Implementation | Status |
|---------|---------------|--------|
| Encryption at Rest | PostgreSQL TDE | ✅ |
| Encryption in Transit | TLS 1.3 | ✅ |
| Secret Management | K8s Secrets / Azure Key Vault | ✅ |
| Log Redaction | PII/secrets removed | ✅ |

### Network Security

| Control | Implementation | Status |
|---------|---------------|--------|
| Ingress Control | Network Policy | ✅ |
| Egress Filtering | Network Policy | ✅ |
| Pod Isolation | Namespace separation | ✅ |
| TLS Termination | Ingress controller | ✅ |

### Monitoring & Alerting

| Metric | Threshold | Action |
|--------|-----------|--------|
| Failed auth attempts | >10/min | Alert + temp block |
| Policy violations | Any | Alert |
| High-risk scan results | Score >70 | Alert + block approval |
| Scanner job failures | >3 consecutive | Alert |

---

## Operational Security Checklist

### Pre-Deployment

- [ ] Review and customize policy configuration
- [ ] Generate strong PostgreSQL passwords
- [ ] Configure Azure AD application registration
- [ ] Set up TLS certificates (Let's Encrypt or internal CA)
- [ ] Enable network policies in K8s cluster
- [ ] Configure Prometheus/Grafana for metrics
- [ ] Set up log aggregation (ELK, Loki, etc.)

### Post-Deployment

- [ ] Test registration workflow end-to-end
- [ ] Verify scan jobs execute successfully
- [ ] Confirm policy enforcement blocks unapproved servers
- [ ] Test audit log generation
- [ ] Validate authentication flows
- [ ] Run penetration test

### Ongoing Operations

- [ ] Weekly: Review audit logs for anomalies
- [ ] Weekly: Check for pending server approvals
- [ ] Monthly: Review and update tool denylists
- [ ] Monthly: Rotate service account credentials
- [ ] Quarterly: Review and update risk thresholds
- [ ] Quarterly: Security assessment of approved servers
- [ ] Annually: Full penetration test

---

## Incident Response

### Suspicious Server Activity

1. **Contain**: Mark server as `Suspended` in registry
2. **Analyze**: Review audit logs for affected time period
3. **Scope**: Identify all users who accessed the server
4. **Remediate**: Revoke approval, rescan, or remove
5. **Report**: Document incident and lessons learned

### Compromised Credentials

1. **Revoke**: Immediately invalidate affected tokens
2. **Rotate**: Generate new secrets/credentials
3. **Audit**: Review actions taken with compromised credentials
4. **Notify**: Inform affected users/teams
5. **Harden**: Implement additional controls (MFA, IP restrictions)

### Scanner Bypass Suspected

1. **Isolate**: Block all traffic to suspected server
2. **Rescan**: Run full scan with additional depth
3. **Manual Review**: Human analysis of server code
4. **Compare**: Check declared vs discovered tools
5. **Improve**: Update scanner patterns if gap found

---

## Compliance Mapping

| Framework | Control | MCP Jurisdiction Coverage |
|-----------|---------|--------------------------|
| SOC 2 | CC6.1 - Logical Access | Registry + RBAC |
| SOC 2 | CC7.2 - Anomaly Detection | Audit Logging |
| ISO 27001 | A.9.4.1 - Access Restriction | Policy Enforcement |
| ISO 27001 | A.12.4.1 - Event Logging | Audit Events |
| NIST CSF | PR.AC-4 - Access Permissions | Team Allowlists |
| NIST CSF | DE.AE-3 - Event Correlation | Audit Query API |

---

## Configuration Hardening

### PostgreSQL

```sql
-- Enforce SSL connections
ALTER SYSTEM SET ssl = on;
ALTER SYSTEM SET ssl_min_protocol_version = 'TLSv1.3';

-- Enable logging
ALTER SYSTEM SET log_connections = on;
ALTER SYSTEM SET log_disconnections = on;
ALTER SYSTEM SET log_statement = 'ddl';

-- Restrict default privileges
REVOKE ALL ON ALL TABLES IN SCHEMA public FROM PUBLIC;
```

### Kubernetes

```yaml
# PodSecurityPolicy / Pod Security Standards
apiVersion: pod-security.kubernetes.io/v1
kind: Namespace
metadata:
  name: mcp-jurisdiction
  labels:
    pod-security.kubernetes.io/enforce: restricted
    pod-security.kubernetes.io/audit: restricted
    pod-security.kubernetes.io/warn: restricted
```

### Ingress

```yaml
# Security headers
annotations:
  nginx.ingress.kubernetes.io/configuration-snippet: |
    add_header X-Frame-Options "DENY" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;
    add_header Content-Security-Policy "default-src 'self'" always;
```

---

## Contact & Reporting

For security issues, contact:
- Internal: security@your-company.com
- MCP Gateway Issues: GitHub Issues on microsoft/mcp-gateway
- MCP-Scan Issues: GitHub Issues on invariantlabs-ai/mcp-scan

**Responsible Disclosure**: Please allow 90 days before public disclosure of any vulnerabilities.
