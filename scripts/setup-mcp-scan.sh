#!/bin/bash
# =============================================================================
# MCP-Scan Developer Setup Script
# =============================================================================
# Distributed by IT/DevOps to developers for one-time setup.
# This script:
#   1. Configures pip to use company Artifactory
#   2. Installs mcp-scan from Artifactory
#   3. Downloads helper scripts
#   4. Sets up environment variables
#
# Usage:
#   curl -sSL https://internal.yourcompany.com/mcp-scan-setup.sh | bash
#   # or
#   ./setup-mcp-scan.sh
#
# =============================================================================

set -euo pipefail

# ================================
# CONFIGURATION - CUSTOMIZE THESE
# ================================
ARTIFACTORY_URL="${MCP_ARTIFACTORY_URL:-https://artifactory.yourcompany.com}"
PYPI_REPO_PATH="api/pypi/python-repo/simple"
GATEWAY_URL="${MCP_GATEWAY_URL:-https://mcp-gateway.yourcompany.com}"
SCRIPTS_URL="${MCP_SCRIPTS_URL:-https://internal.yourcompany.com/mcp-tools}"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo -e "${BLUE}╔═══════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║           MCP-Scan Developer Setup                        ║${NC}"
echo -e "${BLUE}╚═══════════════════════════════════════════════════════════╝${NC}"
echo ""

# Check Python
echo -e "${YELLOW}[1/5] Checking Python installation...${NC}"
if ! command -v python3 &> /dev/null; then
    echo -e "${RED}Error: Python 3 is not installed${NC}"
    echo "Please install Python 3.10+ from your company software center"
    exit 1
fi

PYTHON_VERSION=$(python3 --version | cut -d' ' -f2)
echo -e "${GREEN}✓ Python $PYTHON_VERSION found${NC}"

# Configure pip for Artifactory
echo ""
echo -e "${YELLOW}[2/5] Configuring pip for Artifactory...${NC}"

PIP_CONFIG_DIR="$HOME/.config/pip"
PIP_CONFIG_FILE="$PIP_CONFIG_DIR/pip.conf"

# Windows compatibility
if [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "win32" ]] || [[ -n "${APPDATA:-}" ]]; then
    PIP_CONFIG_DIR="$APPDATA/pip"
    PIP_CONFIG_FILE="$PIP_CONFIG_DIR/pip.ini"
fi

mkdir -p "$PIP_CONFIG_DIR"

# Backup existing config
if [[ -f "$PIP_CONFIG_FILE" ]]; then
    cp "$PIP_CONFIG_FILE" "${PIP_CONFIG_FILE}.backup.$(date +%Y%m%d)"
    echo "Backed up existing pip config"
fi

# Write new config
cat > "$PIP_CONFIG_FILE" << EOF
[global]
index-url = ${ARTIFACTORY_URL}/${PYPI_REPO_PATH}
trusted-host = $(echo "$ARTIFACTORY_URL" | sed 's|https\?://||' | cut -d'/' -f1)

[install]
trusted-host = $(echo "$ARTIFACTORY_URL" | sed 's|https\?://||' | cut -d'/' -f1)
EOF

echo -e "${GREEN}✓ pip configured to use Artifactory${NC}"
echo "  Config file: $PIP_CONFIG_FILE"

# Install mcp-scan
echo ""
echo -e "${YELLOW}[3/5] Installing mcp-scan...${NC}"

if pip install --user mcp-scan; then
    echo -e "${GREEN}✓ mcp-scan installed successfully${NC}"
else
    echo -e "${RED}Failed to install mcp-scan${NC}"
    echo "Check your network connection or VPN"
    exit 1
fi

# Verify installation
MCP_SCAN_VERSION=$(mcp-scan --version 2>/dev/null || echo "unknown")
echo "  Version: $MCP_SCAN_VERSION"

# Download helper scripts
echo ""
echo -e "${YELLOW}[4/5] Setting up helper scripts...${NC}"

TOOLS_DIR="$HOME/.local/bin"
mkdir -p "$TOOLS_DIR"

# Download the upload helper
if command -v curl &> /dev/null; then
    curl -sSL "${SCRIPTS_URL}/local-scan-upload.sh" -o "$TOOLS_DIR/mcp-upload" 2>/dev/null || true
    chmod +x "$TOOLS_DIR/mcp-upload" 2>/dev/null || true
fi

echo -e "${GREEN}✓ Helper scripts installed to $TOOLS_DIR${NC}"

# Set up environment variables
echo ""
echo -e "${YELLOW}[5/5] Setting up environment...${NC}"

# Detect shell config file
SHELL_CONFIG=""
if [[ -f "$HOME/.zshrc" ]]; then
    SHELL_CONFIG="$HOME/.zshrc"
elif [[ -f "$HOME/.bashrc" ]]; then
    SHELL_CONFIG="$HOME/.bashrc"
elif [[ -f "$HOME/.bash_profile" ]]; then
    SHELL_CONFIG="$HOME/.bash_profile"
fi

if [[ -n "$SHELL_CONFIG" ]]; then
    # Check if already configured
    if ! grep -q "MCP_GATEWAY_URL" "$SHELL_CONFIG" 2>/dev/null; then
        cat >> "$SHELL_CONFIG" << EOF

# MCP Jurisdiction Scanner Configuration
export MCP_GATEWAY_URL="${GATEWAY_URL}"
export MCP_PIP_INDEX_URL="${ARTIFACTORY_URL}/${PYPI_REPO_PATH}"
export PATH="\$HOME/.local/bin:\$PATH"
EOF
        echo -e "${GREEN}✓ Environment variables added to $SHELL_CONFIG${NC}"
    else
        echo "Environment already configured in $SHELL_CONFIG"
    fi
fi

# Summary
echo ""
echo -e "${GREEN}╔═══════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║                 Setup Complete!                           ║${NC}"
echo -e "${GREEN}╚═══════════════════════════════════════════════════════════╝${NC}"
echo ""
echo "What's installed:"
echo "  • mcp-scan CLI: $(which mcp-scan 2>/dev/null || echo '$HOME/.local/bin/mcp-scan')"
echo "  • pip config:   $PIP_CONFIG_FILE"
echo "  • Gateway URL:  $GATEWAY_URL"
echo ""
echo -e "${YELLOW}⚠ Restart your terminal or run:${NC}"
echo "  source $SHELL_CONFIG"
echo ""
echo "Quick start:"
echo "  1. Start your local MCP server"
echo "  2. Run: mcp-scan scan --config ~/.config/Claude/claude_desktop_config.json"
echo "  3. Upload results via portal: $GATEWAY_URL"
echo ""
echo "For help: mcp-scan --help"
