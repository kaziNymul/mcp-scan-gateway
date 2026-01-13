# Local MCP Server Scanning Guide

This guide explains how developers can scan MCP servers running on their local machines and upload the results to the governance registry.

## Why Local Scanning?

When your MCP server runs on your laptop (e.g., during development), the Kubernetes-based scanner **cannot reach it**. Instead, you run `mcp-scan` locally and upload the results.

## Prerequisites

1. **Python 3.10+** installed
2. **pip** package manager
3. Your MCP server running locally
4. Access to the MCP Jurisdiction portal
5. Network access to company Artifactory (or VPN)

## Step-by-Step Process

### Step 1: Install MCP-Scan

**From Company Artifactory (Recommended):**

```bash
# One-time setup: Configure pip to use internal repository
pip config set global.index-url https://artifactory.yourcompany.com/api/pypi/python-repo/simple

# Install mcp-scan
pip install mcp-scan
```

Or specify the repository directly:

```bash
pip install mcp-scan --index-url https://artifactory.yourcompany.com/api/pypi/python-repo/simple
```

**With pipx (recommended for CLI tools):**

```bash
pipx install mcp-scan --index-url https://artifactory.yourcompany.com/api/pypi/python-repo/simple
```

**From Public PyPI (if approved):**

```bash
pip install mcp-scan
```

Verify installation:

```bash
mcp-scan --version
```

### Step 2: Create a Scan Configuration

Create a config file for your local server. MCP-Scan uses the same format as Claude Desktop / VS Code.

**Option A: Stdio Server**

Create `~/.config/mcp-scan/my-server.json`:

```json
{
  "mcpServers": {
    "my-local-server": {
      "command": "python",
      "args": ["-m", "my_mcp_server"],
      "env": {
        "LOG_LEVEL": "INFO"
      }
    }
  }
}
```

**Option B: SSE Server (already running)**

```json
{
  "mcpServers": {
    "my-local-server": {
      "url": "http://localhost:3000/sse"
    }
  }
}
```

**Option C: Use existing Claude Desktop config**

MCP-Scan can read your existing Claude Desktop configuration:

```bash
# macOS
mcp-scan scan --config ~/Library/Application\ Support/Claude/claude_desktop_config.json

# Windows
mcp-scan scan --config %APPDATA%\Claude\claude_desktop_config.json

# Linux
mcp-scan scan --config ~/.config/Claude/claude_desktop_config.json
```

### Step 3: Run the Scan

```bash
# Scan with JSON output (required for upload)
mcp-scan scan --config ~/.config/mcp-scan/my-server.json --output-format json > scan-results.json

# View results in terminal
cat scan-results.json | jq .
```

**Sample Output:**

```json
{
  "scan_id": "abc123",
  "timestamp": "2024-01-15T10:30:00Z",
  "scanner_version": "0.2.1",
  "risk_score": 25.5,
  "servers": [
    {
      "name": "my-local-server",
      "status": "connected",
      "tools": [
        {
          "name": "read_file",
          "description": "Read contents of a file",
          "risk_score": 15.0
        },
        {
          "name": "write_file", 
          "description": "Write contents to a file",
          "risk_score": 45.0
        }
      ]
    }
  ],
  "issues": [
    {
      "severity": "medium",
      "type": "tool_risk",
      "message": "Tool 'write_file' can modify filesystem"
    }
  ]
}
```

### Step 4: Register Server in Portal

1. Go to the MCP Jurisdiction portal
2. Click **"Register Server"**
3. Fill in:
   - **Name**: My Local Server
   - **Canonical ID**: `local/my-local-server` (or your preferred ID)
   - **Source Type**: **Local (Developer Machine)**
   - **Owner Team**: Your team name
   - **Declared Tools**: List tools your server provides
4. Click **Register**

### Step 5: Upload Scan Results

After registration, you'll be prompted to upload scan results:

1. Open `scan-results.json` in a text editor
2. Copy the entire JSON content
3. Paste into the **"Scan Results"** text area
4. Click **Upload Scan Results**

### Step 6: Request Approval

Once uploaded, your server will show status **"Scanned Pass"** or **"Scanned Fail"** based on the risk score.

Contact your admin to review and approve the server.

---

## Using the Helper Script

We provide a helper script that automates steps 3-5:

```bash
# Download the helper script
curl -O https://your-company.com/mcp-scan-helper.sh
chmod +x mcp-scan-helper.sh

# Run it
./mcp-scan-helper.sh
```

Or use the script included in this repo:

```bash
./scripts/local-scan-upload.sh my-server.json
```

---

## Troubleshooting

### "Connection refused" error

Your MCP server isn't running. Start it first:

```bash
# For a Python server
python -m my_mcp_server

# For a Node.js server
node dist/index.js
```

### "Invalid JSON" error when uploading

Make sure you're using `--output-format json`:

```bash
mcp-scan scan --config config.json --output-format json > results.json
```

### Scan takes too long

Disable dynamic testing for faster scans:

```bash
mcp-scan scan --config config.json --output-format json --no-dynamic > results.json
```

### "Tool not found" after approval

Make sure the tools in your server match what you declared during registration. Re-scan if you've added new tools.

---

## Re-scanning After Changes

Whenever you modify your MCP server (add tools, change behavior), you should:

1. Run a new scan
2. Upload the new results
3. If significant changes, admin may need to re-approve

```bash
# Quick re-scan and upload
mcp-scan scan --config my-server.json --output-format json | \
  curl -X POST "${GATEWAY_URL}/registry/servers/${SERVER_ID}/scan/upload" \
    -H "Authorization: Bearer ${TOKEN}" \
    -H "Content-Type: application/json" \
    -d @-
```

---

## Security Notes

- **Scan results are not verified**: The system trusts that you ran the scan honestly
- **Admins can request re-scans**: They may ask you to run additional checks
- **Periodic re-scanning**: Consider setting up a reminder to re-scan monthly
- **Don't modify scan output**: Tampering is logged and may result in access revocation
