# Pre-built Packages

This directory contains pre-built packages for offline/enterprise deployment.

## mcp-scan

The MCP security scanner package is included here for convenience.

### Install from this directory

```bash
pip install packages/mcp_scan-0.3.36-py3-none-any.whl
```

### Or upload to your Artifactory

```bash
twine upload \
  --repository-url https://artifactory.yourcompany.com/api/pypi/python-repo \
  packages/mcp_scan-*.whl
```

### Then developers can install from Artifactory

```bash
pip install mcp-scan \
  --index-url https://artifactory.yourcompany.com/api/pypi/python-repo/simple
```

## Building from Source

To build mcp-scan yourself (e.g., for a newer version):

```bash
# Clone the source
git clone https://github.com/invariantlabs-ai/mcp-scan.git

# Build
cd mcp-scan
python3 -m build

# The wheel will be in dist/
ls dist/
# mcp_scan-X.Y.Z-py3-none-any.whl
```

## Package Contents

| File | Description |
|------|-------------|
| `mcp_scan-0.3.36-py3-none-any.whl` | MCP security scanner (Python wheel) |
