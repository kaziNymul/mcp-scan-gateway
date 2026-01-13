# Azure AD Group-Based Access Control Configuration

This guide shows how to configure Azure AD groups for MCP Jurisdiction access control.

## Your AD Group Requirements

| AD Group | Role | Permissions |
|----------|------|-------------|
| `consec-example-read` | `mcp.user` | Register MCP servers, run scans, view UI, upload scan results |
| `consec-example-admin` | `mcp.admin` | All read permissions + Approve/Deny/Suspend servers |

## Step 1: Create App Roles in Azure AD

1. Go to **Azure Portal** → **Entra ID** → **App registrations**
2. Select your MCP Gateway app (e.g., `McpGateway`)
3. Navigate to **App roles** → **Create app role**

### Create User Role

| Field | Value |
|-------|-------|
| Display name | `MCP User` |
| Allowed member types | `Users/Groups` |
| Value | `mcp.user` |
| Description | `Can register and scan MCP servers, view dashboard` |
| ✅ Enable this app role | Checked |

### Create Admin Role (Required)

| Field | Value |
|-------|-------|
| Display name | `MCP Admin` |
| Allowed member types | `Users/Groups` |
| Value | `mcp.admin` |
| Description | `Can approve, deny, and suspend MCP servers` |
| ✅ Enable this app role | Checked |

## Step 2: Assign AD Groups to App Roles

1. In the same app registration, go to **Enterprise applications** (linked)
2. Navigate to **Users and groups** → **Add user/group**

### Assign consec-example-read to mcp.user:

1. Click **Add user/group**
2. Under **Users and groups**, click **None Selected**
3. Search for `consec-example-read`
4. Select the group → **Select**
5. Under **Select a role**, choose **MCP User**
6. Click **Assign**

### Assign consec-example-admin to mcp.admin:

1. Click **Add user/group**
2. Under **Users and groups**, click **None Selected**
3. Search for `consec-example-admin`
4. Select the group → **Select**
5. Under **Select a role**, choose **MCP Admin**
6. Click **Assign**

## Step 3: Configure App Registration Manifest

Ensure group claims are included in the token:

1. Go to **App registrations** → your app → **Manifest**
2. Find `groupMembershipClaims` and set:

```json
{
  "groupMembershipClaims": "ApplicationGroup"
}
```

3. Also verify `optionalClaims` includes roles:

```json
{
  "optionalClaims": {
    "accessToken": [
      {
        "name": "groups",
        "essential": false
      }
    ],
    "idToken": [
      {
        "name": "groups",
        "essential": false
      }
    ]
  }
}
```

4. Click **Save**

## Step 4: Verify Token Claims

After login, the JWT token should contain:

```json
{
  "sub": "user@company.com",
  "name": "John Developer",
  "roles": ["mcp.user"],  // or ["mcp.admin"] for admins
  ...
}
```

## Step 5: Gateway Helm Configuration

Update your production Helm values:

```yaml
# deploy/helm/mcp-jurisdiction/values-production.yaml

auth:
  development:
    enabled: false  # Disable dev mode
  
  oidc:
    enabled: true
    azureAd:
      tenantId: "YOUR_AZURE_TENANT_ID"
      clientId: "YOUR_APP_CLIENT_ID"
    
    # Role claim configuration
    roleClaimPath: "roles"      # JWT path for role claims
    adminRoleValue: "mcp.admin" # Value that grants admin access
    userRoleValue: "mcp.user"   # Value for regular users
```

## Step 6: UI Environment Configuration

For the Next.js admin UI, configure in `.env.local`:

```bash
# Azure AD Configuration
AZURE_AD_CLIENT_ID=your-app-client-id
AZURE_AD_CLIENT_SECRET=your-app-client-secret
AZURE_AD_TENANT_ID=your-tenant-id

# Role Mapping
ADMIN_ROLE_VALUE=mcp.admin
USER_ROLE_VALUE=mcp.user
```

---

## Permissions Matrix

| Action | consec-example-read | consec-example-admin |
|--------|---------------------|----------------------|
| View MCP Servers | ✅ | ✅ |
| Register New Server | ✅ | ✅ |
| Run mcp-scan | ✅ | ✅ |
| Upload Scan Results | ✅ | ✅ |
| View Scan Details | ✅ | ✅ |
| View Audit Logs | ✅ | ✅ |
| **Approve Server** | ❌ | ✅ |
| **Deny Server** | ❌ | ✅ |
| **Suspend Server** | ❌ | ✅ |
| **Configure Policy Settings** | ❌ | ✅ |

---

## How It Works Internally

The gateway uses `ClaimsPrincipal` to check roles:

```csharp
// From ServerRegistryService.cs
private const string AdminRole = "mcp.admin";

private static bool IsAdmin(ClaimsPrincipal user)
{
    var roles = user.GetUserRoles();
    return roles.Any(r => string.Equals(r, AdminRole, StringComparison.OrdinalIgnoreCase));
}
```

When a user tries to approve/deny/suspend:

```csharp
public async Task<Approval> ApproveAsync(ClaimsPrincipal user, Guid serverId, ...)
{
    if (!IsAdmin(user))
    {
        throw new UnauthorizedAccessException("Only administrators can approve servers");
    }
    // ... approval logic
}
```

---

## Testing the Configuration

### 1. Verify AD Group Membership

```powershell
# PowerShell - Check user's groups
Get-AzureADUser -ObjectId user@company.com | Get-AzureADUserMembership
```

### 2. Verify Token Claims

After logging in, decode the JWT token:

```bash
# Browser DevTools → Network → Look for token requests
# Or use https://jwt.io to decode the access token
```

The token should show:
```json
{
  "roles": ["mcp.user"]  // or ["mcp.admin"]
}
```

### 3. Test Permissions

```bash
# As a user (consec-example-read member)
# This should work:
curl -H "Authorization: Bearer $USER_TOKEN" \
  https://mcp-gateway.company.com/registry/servers

# This should fail (403):
curl -H "Authorization: Bearer $USER_TOKEN" \
  -X POST https://mcp-gateway.company.com/registry/servers/uuid/approve

# As an admin (consec-example-admin member)
# This should work:
curl -H "Authorization: Bearer $ADMIN_TOKEN" \
  -X POST https://mcp-gateway.company.com/registry/servers/uuid/approve
```

---

## Troubleshooting

### "User has no roles in token"

1. Check AD group assignment in **Enterprise applications → Users and groups**
2. Verify `groupMembershipClaims` in manifest
3. User may need to sign out and back in

### "Cannot approve - unauthorized"

1. User is not in `consec-example-admin` group
2. Group is not mapped to `mcp.admin` role
3. Token cache - try logging out and back in

### "Group not found in Azure AD"

1. Ensure the group exists in Azure AD (not on-prem only)
2. If synced from on-prem AD, wait for sync cycle
3. Check group type (Security group required, not Distribution list)
