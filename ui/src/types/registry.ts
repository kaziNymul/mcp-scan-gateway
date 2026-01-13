// Server status enum matching backend
export type ServerStatus =
  | 'Draft'
  | 'PendingScan'
  | 'Scanning'
  | 'ScannedPass'
  | 'ScannedFail'
  | 'PendingApproval'
  | 'Approved'
  | 'Denied'
  | 'Deprecated'
  | 'Suspended';

export type SourceType =
  | 'ExternalRepo'
  | 'InternalRepo'
  | 'LocalDeclared'
  | 'ContainerImage'
  | 'PackageArtifact';

export interface ServerRegistration {
  id: string;
  canonicalId: string;
  name: string;
  description?: string;
  ownerTeam: string;
  createdBy: string;
  createdAt: string;
  updatedAt: string;
  status: ServerStatus;
  sourceType: SourceType;
  declaredTools: string[];
  mcpConfig: Record<string, any>;
  latestScanId?: string;
  latestRiskScore?: number;
}

export interface ServerRegistrationRequest {
  canonicalId: string;
  name: string;
  description?: string;
  ownerTeam: string;
  sourceType: SourceType;
  declaredTools: string[];
  mcpConfig: Record<string, any>;
}

export interface ScanResult {
  id: string;
  serverId: string;
  riskScore: number;
  status: string;
  startedAt: string;
  completedAt?: string;
  jobName?: string;
  issues: ScanIssue[];
  discoveredTools: DiscoveredTool[];
  reportJson?: string;
}

export interface ScanIssue {
  severity: 'critical' | 'high' | 'medium' | 'low' | 'info';
  type: string;
  message: string;
  location?: string;
}

export interface DiscoveredTool {
  name: string;
  description?: string;
  inputSchema?: Record<string, any>;
}

export interface Approval {
  id: string;
  serverId: string;
  action: 'Approved' | 'Denied' | 'Deprecated' | 'Suspended' | 'Reinstated' | 'Revoked';
  actorId: string;
  actorEmail?: string;
  justification: string;
  createdAt: string;
}

export interface AuditEvent {
  id: string;
  timestamp: string;
  actorId: string;
  actorEmail?: string;
  team?: string;
  serverCanonicalId: string;
  toolName: string;
  decision: string;
  denyReason?: string;
  latencyMs: number;
  requestSize: number;
  traceId?: string;
}

export interface LocalScanUpload {
  serverId: string;
  scanOutput: string; // JSON output from mcp-scan CLI
  scanVersion: string;
  scannedAt: string;
}
