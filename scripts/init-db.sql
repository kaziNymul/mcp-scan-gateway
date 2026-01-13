-- =============================================================================
-- MCP Jurisdiction Database Initialization
-- =============================================================================

-- Server Registrations
CREATE TABLE IF NOT EXISTS server_registrations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    canonical_id VARCHAR(500) NOT NULL UNIQUE,
    name VARCHAR(255) NOT NULL,
    owner_team VARCHAR(255) NOT NULL,
    source_type VARCHAR(50) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'PendingScan',
    mcp_config JSONB,
    declared_tools JSONB DEFAULT '[]',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Scan Results
CREATE TABLE IF NOT EXISTS scan_results (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    server_id UUID NOT NULL REFERENCES server_registrations(id) ON DELETE CASCADE,
    scanner_version VARCHAR(50),
    risk_score DECIMAL(5,2),
    issues JSONB DEFAULT '[]',
    discovered_tools JSONB DEFAULT '[]',
    raw_output JSONB,
    scanned_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Approvals
CREATE TABLE IF NOT EXISTS approvals (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    server_id UUID NOT NULL REFERENCES server_registrations(id) ON DELETE CASCADE,
    action VARCHAR(50) NOT NULL,
    approved_by VARCHAR(255) NOT NULL,
    reason TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Audit Events
CREATE TABLE IF NOT EXISTS audit_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_type VARCHAR(100) NOT NULL,
    server_id UUID REFERENCES server_registrations(id) ON DELETE SET NULL,
    actor VARCHAR(255),
    details JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Indexes
CREATE INDEX IF NOT EXISTS idx_server_registrations_status ON server_registrations(status);
CREATE INDEX IF NOT EXISTS idx_server_registrations_owner ON server_registrations(owner_team);
CREATE INDEX IF NOT EXISTS idx_scan_results_server ON scan_results(server_id);
CREATE INDEX IF NOT EXISTS idx_scan_results_scanned_at ON scan_results(scanned_at DESC);
CREATE INDEX IF NOT EXISTS idx_approvals_server ON approvals(server_id);
CREATE INDEX IF NOT EXISTS idx_audit_events_type ON audit_events(event_type);
CREATE INDEX IF NOT EXISTS idx_audit_events_created ON audit_events(created_at DESC);

-- Insert sample data for testing
INSERT INTO server_registrations (id, canonical_id, name, owner_team, source_type, status, declared_tools)
VALUES 
    ('11111111-1111-1111-1111-111111111111', 'sample/weather-server', 'Weather Server', 'platform-team', 'ContainerImage', 'Approved', '["get_weather", "get_forecast"]'::jsonb)
ON CONFLICT (canonical_id) DO NOTHING;

-- Log initialization
DO $$
BEGIN
    RAISE NOTICE 'MCP Jurisdiction database initialized successfully';
END $$;
