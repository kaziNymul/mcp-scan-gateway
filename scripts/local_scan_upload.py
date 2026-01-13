#!/usr/bin/env python3
"""
MCP Local Scan & Upload Tool

A cross-platform Python script for scanning local MCP servers and uploading
results to the MCP Jurisdiction registry.

Usage:
    python local_scan_upload.py [config-file] [options]

Examples:
    python local_scan_upload.py                     # Use Claude Desktop config
    python local_scan_upload.py my-server.json      # Use custom config
    python local_scan_upload.py --server-id abc123  # Upload to existing server
"""

import argparse
import json
import os
import platform
import subprocess
import sys
from pathlib import Path
from typing import Optional
import urllib.request
import urllib.error

# ANSI colors
class Colors:
    RED = '\033[0;31m'
    GREEN = '\033[0;32m'
    YELLOW = '\033[1;33m'
    BLUE = '\033[0;34m'
    NC = '\033[0m'  # No Color

    @classmethod
    def disable(cls):
        cls.RED = cls.GREEN = cls.YELLOW = cls.BLUE = cls.NC = ''


def print_color(color: str, message: str):
    print(f"{color}{message}{Colors.NC}")


def print_header():
    print_color(Colors.BLUE, "╔═══════════════════════════════════════════════════════════╗")
    print_color(Colors.BLUE, "║         MCP Local Scan & Upload Tool                      ║")
    print_color(Colors.BLUE, "╚═══════════════════════════════════════════════════════════╝")
    print()


def get_default_config_path() -> Path:
    """Get the default Claude Desktop config path for the current OS."""
    system = platform.system()
    
    if system == "Darwin":  # macOS
        return Path.home() / "Library" / "Application Support" / "Claude" / "claude_desktop_config.json"
    elif system == "Windows":
        appdata = os.environ.get("APPDATA", "")
        return Path(appdata) / "Claude" / "claude_desktop_config.json"
    else:  # Linux and others
        return Path.home() / ".config" / "Claude" / "claude_desktop_config.json"


def check_mcp_scan_installed() -> bool:
    """Check if mcp-scan CLI is installed."""
    try:
        result = subprocess.run(
            ["mcp-scan", "--version"],
            capture_output=True,
            text=True
        )
        return result.returncode == 0
    except FileNotFoundError:
        return False


def get_mcp_scan_version() -> str:
    """Get the installed mcp-scan version."""
    try:
        result = subprocess.run(
            ["mcp-scan", "--version"],
            capture_output=True,
            text=True
        )
        return result.stdout.strip().split('\n')[0]
    except Exception:
        return "unknown"


def run_scan(config_path: Path) -> dict:
    """Run mcp-scan and return the JSON results."""
    print_color(Colors.YELLOW, "[3/5] Running MCP-Scan...")
    print("This may take a minute...")
    
    try:
        result = subprocess.run(
            ["mcp-scan", "scan", "--config", str(config_path), "--output-format", "json"],
            capture_output=True,
            text=True
        )
        
        if result.returncode != 0:
            print_color(Colors.RED, f"Scan failed: {result.stderr}")
            sys.exit(1)
        
        scan_results = json.loads(result.stdout)
        print_color(Colors.GREEN, "✓ Scan completed successfully")
        
        return scan_results
        
    except json.JSONDecodeError as e:
        print_color(Colors.RED, f"Failed to parse scan output: {e}")
        sys.exit(1)
    except FileNotFoundError:
        print_color(Colors.RED, "mcp-scan not found. Install with: pip install mcp-scan")
        sys.exit(1)


def api_request(url: str, token: str, method: str = "GET", data: Optional[dict] = None) -> dict:
    """Make an API request to the gateway."""
    headers = {
        "Authorization": f"Bearer {token}",
        "Content-Type": "application/json",
    }
    
    body = json.dumps(data).encode() if data else None
    
    req = urllib.request.Request(url, data=body, headers=headers, method=method)
    
    try:
        with urllib.request.urlopen(req) as response:
            return json.loads(response.read().decode())
    except urllib.error.HTTPError as e:
        error_body = e.read().decode()
        try:
            return json.loads(error_body)
        except json.JSONDecodeError:
            return {"error": error_body}
    except urllib.error.URLError as e:
        return {"error": str(e.reason)}


def register_server(
    gateway_url: str,
    token: str,
    server_name: str,
    owner_team: str,
    config_path: str,
    declared_tools: list
) -> Optional[str]:
    """Register a new server and return the server ID."""
    print_color(Colors.YELLOW, "[4/5] Registering server...")
    
    canonical_id = f"local/{server_name}"
    
    data = {
        "canonicalId": canonical_id,
        "name": server_name,
        "ownerTeam": owner_team,
        "sourceType": "LocalDeclared",
        "declaredTools": declared_tools,
        "mcpConfig": {
            "transport": "stdio",
            "configFile": str(config_path),
        }
    }
    
    response = api_request(f"{gateway_url}/registry/servers", token, "POST", data)
    
    if "id" in response:
        print_color(Colors.GREEN, f"✓ Server registered: {response['id']}")
        return response["id"]
    else:
        print_color(Colors.RED, f"Failed to register: {response.get('error', response)}")
        return None


def upload_scan(gateway_url: str, token: str, server_id: str, scan_results: dict) -> bool:
    """Upload scan results to the registry."""
    print_color(Colors.YELLOW, "[5/5] Uploading scan results...")
    
    from datetime import datetime
    
    data = {
        "scanOutput": json.dumps(scan_results),
        "scanVersion": get_mcp_scan_version(),
        "scannedAt": datetime.utcnow().isoformat() + "Z",
    }
    
    response = api_request(
        f"{gateway_url}/registry/servers/{server_id}/scan/upload",
        token,
        "POST",
        data
    )
    
    if "id" in response:
        print_color(Colors.GREEN, "✓ Scan uploaded successfully")
        return True
    else:
        print_color(Colors.RED, f"Failed to upload: {response.get('error', response)}")
        return False


def main():
    parser = argparse.ArgumentParser(
        description="Scan local MCP servers and upload results to governance registry"
    )
    parser.add_argument(
        "config",
        nargs="?",
        help="Path to MCP config file (default: Claude Desktop config)"
    )
    parser.add_argument(
        "--server-id",
        help="Upload to existing server (skip registration)"
    )
    parser.add_argument(
        "--name",
        help="Server name for registration"
    )
    parser.add_argument(
        "--team",
        help="Owner team for registration"
    )
    parser.add_argument(
        "--gateway",
        default=os.environ.get("MCP_GATEWAY_URL", "http://localhost:8000"),
        help="Gateway API URL"
    )
    parser.add_argument(
        "--token",
        default=os.environ.get("MCP_TOKEN", ""),
        help="Authentication token"
    )
    parser.add_argument(
        "--no-color",
        action="store_true",
        help="Disable colored output"
    )
    
    args = parser.parse_args()
    
    if args.no_color:
        Colors.disable()
    
    print_header()
    
    # Check prerequisites
    print_color(Colors.YELLOW, "[1/5] Checking prerequisites...")
    
    if not check_mcp_scan_installed():
        print_color(Colors.RED, "Error: mcp-scan is not installed")
        print("\nInstall it with:")
        print("  pip install mcp-scan")
        sys.exit(1)
    
    if not args.token:
        print_color(Colors.RED, "Error: Authentication token required")
        print("\nSet MCP_TOKEN environment variable or use --token")
        sys.exit(1)
    
    print_color(Colors.GREEN, "✓ All prerequisites met")
    
    # Load config
    print()
    print_color(Colors.YELLOW, "[2/5] Loading configuration...")
    
    config_path = Path(args.config) if args.config else get_default_config_path()
    
    if not config_path.exists():
        print_color(Colors.RED, f"Error: Config file not found: {config_path}")
        sys.exit(1)
    
    print(f"Config file: {config_path}")
    
    with open(config_path) as f:
        config = json.load(f)
    
    servers = list(config.get("mcpServers", {}).keys())
    if servers:
        print("Servers found:")
        for server in servers:
            print(f"  - {server}")
    
    # Run scan
    print()
    scan_results = run_scan(config_path)
    
    # Display results
    risk_score = scan_results.get("risk_score", 0)
    tools = scan_results.get("servers", [{}])[0].get("tools", [])
    issues = scan_results.get("issues", [])
    
    print()
    print("Scan Results:")
    print(f"  Risk Score: {risk_score}")
    print(f"  Tools Found: {len(tools)}")
    print(f"  Issues: {len(issues)}")
    
    # Get server ID (register if needed)
    print()
    server_id = args.server_id
    
    if not server_id:
        # Get server name
        server_name = args.name
        if not server_name:
            default_name = servers[0] if servers else "my-local-server"
            server_name = input(f"Server name [{default_name}]: ").strip() or default_name
        
        # Get owner team
        owner_team = args.team
        if not owner_team:
            import getpass
            default_team = getpass.getuser()
            owner_team = input(f"Owner team [{default_team}]: ").strip() or default_team
        
        # Extract tool names
        declared_tools = [tool.get("name", "") for tool in tools]
        
        server_id = register_server(
            args.gateway,
            args.token,
            server_name,
            owner_team,
            str(config_path),
            declared_tools
        )
        
        if not server_id:
            sys.exit(1)
    else:
        print_color(Colors.YELLOW, f"[4/5] Using existing server: {server_id}")
    
    # Upload scan
    print()
    success = upload_scan(args.gateway, args.token, server_id, scan_results)
    
    if not success:
        sys.exit(1)
    
    # Summary
    print()
    print_color(Colors.GREEN, "╔═══════════════════════════════════════════════════════════╗")
    print_color(Colors.GREEN, "║                    Scan Complete!                         ║")
    print_color(Colors.GREEN, "╚═══════════════════════════════════════════════════════════╝")
    print()
    print(f"Server ID:    {server_id}")
    print(f"Risk Score:   {risk_score}")
    print()
    print("Next steps:")
    if risk_score <= 50:
        print("  ✓ Scan passed! Request admin approval.")
    else:
        print("  ⚠ High risk score. Review issues before requesting approval.")
    print()
    print(f"View in portal: {args.gateway.replace('api.', '')}/dashboard")


if __name__ == "__main__":
    main()
