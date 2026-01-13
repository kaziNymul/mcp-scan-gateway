"""
Mock MCP Gateway API
====================
A simplified Python implementation of the MCP Jurisdiction Gateway API
for local testing without needing to build the .NET application.
"""

import uuid
import json
from datetime import datetime
from typing import Optional
from contextlib import asynccontextmanager

from fastapi import FastAPI, HTTPException, Query, Request, Response
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import StreamingResponse
from pydantic import BaseModel
import asyncpg
import httpx
import os

# Database connection
DATABASE_URL = os.environ.get(
    "DATABASE_URL",
    "postgresql://mcp_admin:localdev123@localhost:5432/mcp_jurisdiction"
)

# Policy settings
POLICY_MAX_RISK_SCORE = float(os.environ.get("POLICY_MAX_RISK_SCORE", "75"))
POLICY_AUTO_APPROVE_BELOW = float(os.environ.get("POLICY_AUTO_APPROVE_BELOW", "25"))


# Models
class ServerRegistrationRequest(BaseModel):
    canonicalId: str
    name: str
    ownerTeam: str
    sourceType: str = "LocalDeclared"
    declaredTools: list[str] = []
    mcpConfig: Optional[dict] = None


class LocalScanUploadRequest(BaseModel):
    scanOutput: str
    scanVersion: str = "unknown"
    scannedAt: Optional[str] = None


class ApprovalRequest(BaseModel):
    reason: Optional[str] = None


# Database pool
db_pool = None


@asynccontextmanager
async def lifespan(app: FastAPI):
    global db_pool
    db_pool = await asyncpg.create_pool(DATABASE_URL)
    yield
    await db_pool.close()


app = FastAPI(
    title="MCP Jurisdiction Gateway (Test)",
    description="Mock gateway for local testing",
    lifespan=lifespan
)

# CORS
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.get("/health")
async def health():
    return {"status": "healthy", "version": "test-1.0.0"}


# =============================================================================
# Server Registration Endpoints
# =============================================================================

@app.get("/registry/servers")
async def list_servers(
    status: Optional[str] = None,
    owner: Optional[str] = None
):
    """List all registered servers."""
    query = "SELECT * FROM server_registrations WHERE 1=1"
    params = []
    
    if status:
        params.append(status)
        query += f" AND status = ${len(params)}"
    if owner:
        params.append(owner)
        query += f" AND owner_team = ${len(params)}"
    
    query += " ORDER BY created_at DESC"
    
    async with db_pool.acquire() as conn:
        rows = await conn.fetch(query, *params)
        
    return [dict(row) for row in rows]


@app.get("/registry/servers/{server_id}")
async def get_server(server_id: str):
    """Get a specific server."""
    async with db_pool.acquire() as conn:
        row = await conn.fetchrow(
            "SELECT * FROM server_registrations WHERE id = $1",
            uuid.UUID(server_id)
        )
        
    if not row:
        raise HTTPException(status_code=404, detail="Server not found")
    
    return dict(row)


@app.post("/registry/servers")
async def register_server(req: ServerRegistrationRequest):
    """Register a new MCP server."""
    server_id = uuid.uuid4()
    now = datetime.utcnow()
    
    async with db_pool.acquire() as conn:
        # Check if already exists
        existing = await conn.fetchrow(
            "SELECT id FROM server_registrations WHERE canonical_id = $1",
            req.canonicalId
        )
        if existing:
            raise HTTPException(status_code=409, detail="Server already registered")
        
        # Insert
        await conn.execute("""
            INSERT INTO server_registrations 
            (id, canonical_id, name, owner_team, source_type, status, declared_tools, mcp_config, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $9)
        """, 
            server_id,
            req.canonicalId,
            req.name,
            req.ownerTeam,
            req.sourceType,
            "PendingScan",
            json.dumps(req.declaredTools),
            json.dumps(req.mcpConfig) if req.mcpConfig else None,
            now
        )
        
        # Audit log
        await conn.execute("""
            INSERT INTO audit_events (event_type, server_id, actor, details, created_at)
            VALUES ($1, $2, $3, $4, $5)
        """,
            "ServerRegistered",
            server_id,
            "test-user",
            json.dumps({"name": req.name, "canonicalId": req.canonicalId}),
            now
        )
    
    return {
        "id": str(server_id),
        "canonicalId": req.canonicalId,
        "name": req.name,
        "status": "PendingScan",
        "message": "Server registered. Upload scan results to complete registration."
    }


@app.post("/registry/servers/{server_id}/scan/upload")
async def upload_scan(server_id: str, req: LocalScanUploadRequest):
    """Upload local scan results."""
    scan_id = uuid.uuid4()
    now = datetime.utcnow()
    
    # Parse scan output
    try:
        scan_data = json.loads(req.scanOutput)
    except json.JSONDecodeError:
        raise HTTPException(status_code=400, detail="Invalid JSON in scanOutput")
    
    risk_score = scan_data.get("risk_score", 0)
    issues = scan_data.get("issues", [])
    tools = []
    
    # Extract tools from scan
    for server in scan_data.get("servers", []):
        for tool in server.get("tools", []):
            tools.append(tool.get("name", "unknown"))
    
    # Determine new status based on risk score
    if risk_score <= POLICY_AUTO_APPROVE_BELOW:
        new_status = "Approved"
    elif risk_score <= POLICY_MAX_RISK_SCORE:
        new_status = "ScannedPass"
    else:
        new_status = "ScannedFail"
    
    async with db_pool.acquire() as conn:
        # Check server exists
        server = await conn.fetchrow(
            "SELECT * FROM server_registrations WHERE id = $1",
            uuid.UUID(server_id)
        )
        if not server:
            raise HTTPException(status_code=404, detail="Server not found")
        
        # Insert scan result
        await conn.execute("""
            INSERT INTO scan_results 
            (id, server_id, scanner_version, risk_score, issues, discovered_tools, raw_output, scanned_at, created_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
        """,
            scan_id,
            uuid.UUID(server_id),
            req.scanVersion,
            risk_score,
            json.dumps(issues),
            json.dumps(tools),
            json.dumps(scan_data),
            datetime.fromisoformat(req.scannedAt.replace("Z", "+00:00")) if req.scannedAt else now,
            now
        )
        
        # Update server status
        await conn.execute("""
            UPDATE server_registrations 
            SET status = $1, updated_at = $2
            WHERE id = $3
        """, new_status, now, uuid.UUID(server_id))
        
        # Audit log
        await conn.execute("""
            INSERT INTO audit_events (event_type, server_id, actor, details, created_at)
            VALUES ($1, $2, $3, $4, $5)
        """,
            "ScanUploaded",
            uuid.UUID(server_id),
            "test-user",
            json.dumps({"riskScore": risk_score, "toolCount": len(tools), "newStatus": new_status}),
            now
        )
    
    return {
        "id": str(scan_id),
        "serverId": server_id,
        "riskScore": risk_score,
        "status": new_status,
        "toolsFound": len(tools),
        "issuesFound": len(issues),
        "message": f"Scan uploaded. Server status: {new_status}"
    }


@app.get("/registry/servers/{server_id}/scans")
async def get_scans(server_id: str):
    """Get scan history for a server."""
    async with db_pool.acquire() as conn:
        rows = await conn.fetch(
            "SELECT * FROM scan_results WHERE server_id = $1 ORDER BY scanned_at DESC",
            uuid.UUID(server_id)
        )
    
    return [dict(row) for row in rows]


@app.post("/registry/servers/{server_id}/approve")
async def approve_server(server_id: str, req: ApprovalRequest):
    """Approve a server."""
    return await _update_server_status(server_id, "Approved", "approve", req.reason)


@app.post("/registry/servers/{server_id}/deny")
async def deny_server(server_id: str, req: ApprovalRequest):
    """Deny a server."""
    return await _update_server_status(server_id, "Denied", "deny", req.reason)


@app.post("/registry/servers/{server_id}/suspend")
async def suspend_server(server_id: str, req: ApprovalRequest):
    """Suspend a server."""
    return await _update_server_status(server_id, "Suspended", "suspend", req.reason)


async def _update_server_status(server_id: str, new_status: str, action: str, reason: Optional[str]):
    """Helper to update server status."""
    now = datetime.utcnow()
    approval_id = uuid.uuid4()
    
    async with db_pool.acquire() as conn:
        # Check exists
        server = await conn.fetchrow(
            "SELECT * FROM server_registrations WHERE id = $1",
            uuid.UUID(server_id)
        )
        if not server:
            raise HTTPException(status_code=404, detail="Server not found")
        
        # Update status
        await conn.execute("""
            UPDATE server_registrations SET status = $1, updated_at = $2 WHERE id = $3
        """, new_status, now, uuid.UUID(server_id))
        
        # Record approval
        await conn.execute("""
            INSERT INTO approvals (id, server_id, action, approved_by, reason, created_at)
            VALUES ($1, $2, $3, $4, $5, $6)
        """, approval_id, uuid.UUID(server_id), action, "test-admin", reason, now)
        
        # Audit
        await conn.execute("""
            INSERT INTO audit_events (event_type, server_id, actor, details, created_at)
            VALUES ($1, $2, $3, $4, $5)
        """,
            f"Server{action.capitalize()}d",
            uuid.UUID(server_id),
            "test-admin",
            json.dumps({"reason": reason, "previousStatus": server["status"]}),
            now
        )
    
    return {
        "id": server_id,
        "status": new_status,
        "message": f"Server {action}d successfully"
    }


# =============================================================================
# Audit Endpoints
# =============================================================================

@app.get("/audit/events")
async def get_audit_events(
    event_type: Optional[str] = None,
    server_id: Optional[str] = None,
    limit: int = Query(default=100, le=1000)
):
    """Get audit events."""
    query = "SELECT * FROM audit_events WHERE 1=1"
    params = []
    
    if event_type:
        params.append(event_type)
        query += f" AND event_type = ${len(params)}"
    if server_id:
        params.append(uuid.UUID(server_id))
        query += f" AND server_id = ${len(params)}"
    
    params.append(limit)
    query += f" ORDER BY created_at DESC LIMIT ${len(params)}"
    
    async with db_pool.acquire() as conn:
        rows = await conn.fetch(query, *params)
    
    return [dict(row) for row in rows]


# =============================================================================
# Policy Check Endpoint (simulates middleware)
# =============================================================================

@app.get("/policy/check")
async def check_policy(server_url: str):
    """Check if a server is allowed by policy."""
    async with db_pool.acquire() as conn:
        # Check if server is registered and approved
        row = await conn.fetchrow("""
            SELECT id, canonical_id, name, status 
            FROM server_registrations 
            WHERE canonical_id = $1 OR mcp_config->>'url' = $1
        """, server_url)
    
    if not row:
        return {
            "allowed": False,
            "reason": "Server not registered",
            "action": "block"
        }
    
    if row["status"] != "Approved":
        return {
            "allowed": False,
            "reason": f"Server status is {row['status']}, not Approved",
            "action": "block"
        }
    
    return {
        "allowed": True,
        "serverId": str(row["id"]),
        "serverName": row["name"]
    }


# =============================================================================
# MCP Proxy Endpoint - For GitHub Copilot / VS Code Integration
# =============================================================================
# This is the key integration point. Configure VS Code to use:
#   "mcpServers": {
#     "my-server": {
#       "url": "https://gateway.company.com/mcp/proxy/my-server-id"
#     }
#   }
# The gateway checks if the server is approved before proxying.

async def get_server_target(server_id: str) -> tuple[dict, str]:
    """Get server config and validate it's approved."""
    async with db_pool.acquire() as conn:
        row = await conn.fetchrow("""
            SELECT id, canonical_id, name, status, mcp_config 
            FROM server_registrations 
            WHERE id::text = $1 OR canonical_id = $1
        """, server_id)
    
    if not row:
        raise HTTPException(status_code=404, detail="MCP server not registered")
    
    if row["status"] != "Approved":
        raise HTTPException(
            status_code=403, 
            detail=f"MCP server '{row['name']}' is not approved (status: {row['status']})"
        )
    
    config = json.loads(row["mcp_config"]) if row["mcp_config"] else {}
    return dict(row), config


@app.api_route("/mcp/proxy/{server_id:path}", methods=["GET", "POST", "PUT", "DELETE"])
async def mcp_proxy(server_id: str, request: Request):
    """
    Proxy MCP requests to approved servers only.
    
    GitHub Copilot / VS Code will connect to this endpoint.
    We validate the server is approved, then forward the request.
    """
    # Extract actual server ID (may have path suffix like /sse or /messages)
    parts = server_id.split("/", 1)
    actual_server_id = parts[0]
    path_suffix = "/" + parts[1] if len(parts) > 1 else ""
    
    # Validate server is approved
    server, config = await get_server_target(actual_server_id)
    
    # Get target URL from config
    target_url = config.get("url") or config.get("endpoint")
    if not target_url:
        raise HTTPException(
            status_code=400, 
            detail="Server has no remote URL configured. Local servers must be scanned locally."
        )
    
    # Build full target URL
    full_target = target_url.rstrip("/") + path_suffix
    
    # Log the proxy request
    print(f"[MCP Proxy] {server['name']} -> {full_target}")
    
    # Forward the request
    async with httpx.AsyncClient(timeout=60.0) as client:
        # Get request body if present
        body = await request.body()
        
        # Forward headers (filter out host)
        headers = {
            k: v for k, v in request.headers.items() 
            if k.lower() not in ("host", "content-length")
        }
        
        try:
            # Check if this is an SSE request
            if "text/event-stream" in request.headers.get("accept", ""):
                # Stream SSE responses
                async def stream_sse():
                    async with client.stream(
                        request.method,
                        full_target,
                        headers=headers,
                        content=body
                    ) as response:
                        async for chunk in response.aiter_bytes():
                            yield chunk
                
                return StreamingResponse(
                    stream_sse(),
                    media_type="text/event-stream",
                    headers={"Cache-Control": "no-cache", "Connection": "keep-alive"}
                )
            else:
                # Regular request
                response = await client.request(
                    method=request.method,
                    url=full_target,
                    headers=headers,
                    content=body
                )
                
                return Response(
                    content=response.content,
                    status_code=response.status_code,
                    headers=dict(response.headers)
                )
                
        except httpx.ConnectError:
            raise HTTPException(status_code=502, detail=f"Cannot connect to MCP server: {target_url}")
        except httpx.TimeoutException:
            raise HTTPException(status_code=504, detail="MCP server timeout")


@app.get("/mcp/servers")
async def list_approved_mcp_servers():
    """
    List all approved MCP servers that can be used.
    VS Code extensions can call this to show available servers.
    """
    async with db_pool.acquire() as conn:
        rows = await conn.fetch("""
            SELECT id, canonical_id, name, declared_tools, mcp_config
            FROM server_registrations 
            WHERE status = 'Approved'
            ORDER BY name
        """)
    
    servers = []
    for row in rows:
        config = json.loads(row["mcp_config"]) if row["mcp_config"] else {}
        tools = json.loads(row["declared_tools"]) if row["declared_tools"] else []
        
        # Determine if this is a proxiable server
        has_remote_url = bool(config.get("url") or config.get("endpoint"))
        
        servers.append({
            "id": str(row["id"]),
            "canonicalId": row["canonical_id"],
            "name": row["name"],
            "tools": tools,
            "proxyUrl": f"/mcp/proxy/{row['id']}" if has_remote_url else None,
            "isLocal": not has_remote_url,
            "note": "Local server - run locally" if not has_remote_url else None
        })
    
    return {"servers": servers}


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8080)
