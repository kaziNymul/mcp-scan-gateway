# MCP-Scan Source Code

This directory contains the source code for mcp-scan, the MCP security scanning tool from [Invariant Labs](https://github.com/invariantlabs-ai/mcp-scan).

## Version

Current version: **0.3.36**

## Building from Source

```bash
cd mcp-scan-source
python3 -m pip install build
python3 -m build
```

This creates:
- `dist/mcp_scan-0.3.36-py3-none-any.whl` (wheel package)
- `dist/mcp_scan-0.3.36.tar.gz` (source distribution)

## Pre-built Package

A pre-built wheel is available in the `packages/` directory at the root of this repository:

```bash
pip install packages/mcp_scan-0.3.36-py3-none-any.whl
```

## Installing for Development

```bash
cd mcp-scan-source
pip install -e .
```

## Usage

```bash
# Scan an MCP config file
mcp-scan scan ~/.vscode/mcp.json

# Inspect tools without verification
mcp-scan inspect ~/.vscode/mcp.json

# Output as JSON (for uploading to gateway)
mcp-scan scan ~/.vscode/mcp.json --json > scan-results.json
```

## Uploading to Enterprise Artifactory

```bash
# Upload the wheel to your Artifactory PyPI repository
twine upload \
  --repository-url https://artifactory.yourcompany.com/api/pypi/python-repo \
  dist/mcp_scan-0.3.36-py3-none-any.whl

# Developers can then install from Artifactory
pip install mcp-scan \
  --index-url https://artifactory.yourcompany.com/api/pypi/python-repo/simple
```

## License

Apache-2.0 - See [LICENSE](LICENSE) for details.

## Original Repository

https://github.com/invariantlabs-ai/mcp-scan
