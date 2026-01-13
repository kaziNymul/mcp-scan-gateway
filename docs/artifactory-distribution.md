# MCP-Scan Distribution via Artifactory

This guide explains how to host MCP-Scan in your company's Artifactory and distribute it to developers.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                           Artifactory                                │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │  python-repo (PyPI-compatible)                                │  │
│  │    └── mcp-scan-0.2.1.tar.gz                                  │  │
│  │    └── mcp-scan-0.2.1-py3-none-any.whl                        │  │
│  └───────────────────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │  generic-repo (Scripts)                                       │  │
│  │    └── setup-mcp-scan.sh                                      │  │
│  │    └── local-scan-upload.sh                                   │  │
│  └───────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
                                │
                         pip install
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      Developer Machines                              │
│                                                                      │
│   ~/.local/bin/mcp-scan         (CLI tool)                          │
│   ~/.config/pip/pip.conf        (Artifactory config)                │
│   ~/.bashrc                     (environment variables)             │
└─────────────────────────────────────────────────────────────────────┘
```

## Step 1: Create PyPI Repository in Artifactory

1. Log into Artifactory admin console
2. Go to **Admin → Repositories → Local**
3. Click **New Local Repository**
4. Select **PyPI** as the package type
5. Name it: `python-repo`
6. Click **Create**

## Step 2: Upload MCP-Scan Package

### Option A: From GitHub Release

```bash
# Download latest release
wget https://github.com/invariantlabs-ai/mcp-scan/releases/latest/download/mcp_scan-*.whl

# Upload to Artifactory
curl -u admin:password -T mcp_scan-*.whl \
  "https://artifactory.yourcompany.com/artifactory/python-repo/"
```

### Option B: Build from Source

```bash
# Clone and build
git clone https://github.com/invariantlabs-ai/mcp-scan.git
cd mcp-scan
pip install build
python -m build

# Upload using twine
pip install twine
twine upload \
  --repository-url https://artifactory.yourcompany.com/api/pypi/python-repo/ \
  --username admin \
  --password $ARTIFACTORY_PASSWORD \
  dist/*
```

### Option C: Mirror Public PyPI (Recommended)

1. Create a **Remote Repository** in Artifactory pointing to `https://pypi.org`
2. Create a **Virtual Repository** combining local + remote
3. Developers get mcp-scan automatically, cached in Artifactory

## Step 3: Upload Helper Scripts

```bash
# Upload setup script
curl -u admin:password -T scripts/setup-mcp-scan.sh \
  "https://artifactory.yourcompany.com/artifactory/generic-repo/mcp-tools/"

# Upload scan helper
curl -u admin:password -T scripts/local-scan-upload.sh \
  "https://artifactory.yourcompany.com/artifactory/generic-repo/mcp-tools/"
```

## Step 4: Customize Setup Script

Edit `scripts/setup-mcp-scan.sh` and update these variables:

```bash
ARTIFACTORY_URL="https://artifactory.yourcompany.com"
PYPI_REPO_PATH="api/pypi/python-repo/simple"
GATEWAY_URL="https://mcp-gateway.yourcompany.com"
SCRIPTS_URL="https://artifactory.yourcompany.com/artifactory/generic-repo/mcp-tools"
```

## Step 5: Distribute to Developers

### Option A: One-liner Install

Share this with developers:

```bash
curl -sSL https://artifactory.yourcompany.com/artifactory/generic-repo/mcp-tools/setup-mcp-scan.sh | bash
```

### Option B: Internal Documentation

Add to your developer onboarding docs:

```markdown
## MCP Server Scanning Setup

1. Run the setup script:
   ```bash
   curl -sSL https://internal.yourcompany.com/mcp-tools/setup-mcp-scan.sh | bash
   ```

2. Restart your terminal

3. Scan your MCP servers:
   ```bash
   mcp-scan scan --config ~/.config/Claude/claude_desktop_config.json --output-format json
   ```

4. Upload results at: https://mcp-gateway.yourcompany.com
```

### Option C: Company Software Center

Package the setup script as:
- **Windows**: MSI installer
- **macOS**: PKG installer  
- **Linux**: DEB/RPM package

## Ongoing Maintenance

### Updating MCP-Scan

When a new version is released:

```bash
# Download new version
pip download mcp-scan --no-deps -d ./dist

# Upload to Artifactory
twine upload \
  --repository-url https://artifactory.yourcompany.com/api/pypi/python-repo/ \
  dist/mcp_scan-*.whl
```

### Monitoring Usage

Check Artifactory download stats:
- **Admin → Artifacts → python-repo → mcp-scan**
- View download count and recent access

### Version Pinning (Optional)

For consistency, pin the version in setup script:

```bash
pip install mcp-scan==0.2.1
```

## Security Considerations

1. **Package Integrity**: Verify checksums before uploading
2. **Access Control**: Restrict who can upload to python-repo
3. **Audit Logging**: Enable Artifactory audit logs
4. **Vulnerability Scanning**: Enable Xray scanning for the repository

## Troubleshooting

### "Package not found"

```bash
# Check repository is accessible
pip index versions mcp-scan --index-url https://artifactory.yourcompany.com/api/pypi/python-repo/simple
```

### "SSL Certificate Error"

```bash
# Add trusted host
pip install mcp-scan --trusted-host artifactory.yourcompany.com
```

### "Permission Denied"

```bash
# Install in user directory
pip install --user mcp-scan
```

### Check pip configuration

```bash
pip config list
```
