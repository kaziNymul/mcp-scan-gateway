import { getSession } from 'next-auth/react';
import {
  ServerRegistration,
  ServerRegistrationRequest,
  ScanResult,
  AuditEvent,
  LocalScanUpload,
} from '@/types/registry';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8000';

async function fetchWithAuth(url: string, options: RequestInit = {}) {
  const session = await getSession();
  
  if (!session?.accessToken) {
    throw new Error('Not authenticated');
  }

  const response = await fetch(`${API_URL}${url}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${session.accessToken}`,
      ...options.headers,
    },
  });

  if (!response.ok) {
    const error = await response.text();
    throw new Error(error || `HTTP ${response.status}`);
  }

  if (response.status === 204) {
    return null;
  }

  return response.json();
}

// Server Registry API
export const registryApi = {
  // List all servers (with optional filters)
  listServers: async (filters?: {
    status?: string;
    ownerTeam?: string;
    search?: string;
  }): Promise<ServerRegistration[]> => {
    const params = new URLSearchParams();
    if (filters?.status) params.set('status', filters.status);
    if (filters?.ownerTeam) params.set('ownerTeam', filters.ownerTeam);
    if (filters?.search) params.set('search', filters.search);
    
    const query = params.toString();
    return fetchWithAuth(`/registry/servers${query ? `?${query}` : ''}`);
  },

  // Get single server
  getServer: async (id: string): Promise<ServerRegistration> => {
    return fetchWithAuth(`/registry/servers/${id}`);
  },

  // Register new server
  registerServer: async (
    request: ServerRegistrationRequest
  ): Promise<ServerRegistration> => {
    return fetchWithAuth('/registry/servers', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  },

  // Update server
  updateServer: async (
    id: string,
    request: Partial<ServerRegistrationRequest>
  ): Promise<ServerRegistration> => {
    return fetchWithAuth(`/registry/servers/${id}`, {
      method: 'PUT',
      body: JSON.stringify(request),
    });
  },

  // Delete server
  deleteServer: async (id: string): Promise<void> => {
    return fetchWithAuth(`/registry/servers/${id}`, {
      method: 'DELETE',
    });
  },

  // Trigger scan (for container/remote servers)
  triggerScan: async (id: string): Promise<{ jobName: string }> => {
    return fetchWithAuth(`/registry/servers/${id}/scan`, {
      method: 'POST',
    });
  },

  // Upload local scan results
  uploadLocalScan: async (upload: LocalScanUpload): Promise<ScanResult> => {
    return fetchWithAuth(`/registry/servers/${upload.serverId}/scan/upload`, {
      method: 'POST',
      body: JSON.stringify(upload),
    });
  },

  // Get scan results
  getScans: async (serverId: string): Promise<ScanResult[]> => {
    return fetchWithAuth(`/registry/servers/${serverId}/scans`);
  },

  // Get specific scan
  getScan: async (serverId: string, scanId: string): Promise<ScanResult> => {
    return fetchWithAuth(`/registry/servers/${serverId}/scans/${scanId}`);
  },

  // Approve server (admin only)
  approveServer: async (
    id: string,
    justification: string
  ): Promise<ServerRegistration> => {
    return fetchWithAuth(`/registry/servers/${id}/approve`, {
      method: 'POST',
      body: JSON.stringify({ justification }),
    });
  },

  // Deny server (admin only)
  denyServer: async (
    id: string,
    justification: string
  ): Promise<ServerRegistration> => {
    return fetchWithAuth(`/registry/servers/${id}/deny`, {
      method: 'POST',
      body: JSON.stringify({ justification }),
    });
  },

  // Suspend server (admin only)
  suspendServer: async (
    id: string,
    justification: string
  ): Promise<ServerRegistration> => {
    return fetchWithAuth(`/registry/servers/${id}/suspend`, {
      method: 'POST',
      body: JSON.stringify({ justification }),
    });
  },
};

// Audit API
export const auditApi = {
  // Query audit events
  queryEvents: async (filters?: {
    serverCanonicalId?: string;
    actorId?: string;
    decision?: string;
    startTime?: string;
    endTime?: string;
    limit?: number;
  }): Promise<AuditEvent[]> => {
    const params = new URLSearchParams();
    if (filters?.serverCanonicalId)
      params.set('serverCanonicalId', filters.serverCanonicalId);
    if (filters?.actorId) params.set('actorId', filters.actorId);
    if (filters?.decision) params.set('decision', filters.decision);
    if (filters?.startTime) params.set('startTime', filters.startTime);
    if (filters?.endTime) params.set('endTime', filters.endTime);
    if (filters?.limit) params.set('limit', filters.limit.toString());
    
    const query = params.toString();
    return fetchWithAuth(`/registry/audit${query ? `?${query}` : ''}`);
  },

  // Get audit stats
  getStats: async (): Promise<{
    totalEvents: number;
    allowedCount: number;
    deniedCount: number;
    topDeniedServers: { canonicalId: string; count: number }[];
  }> => {
    return fetchWithAuth('/registry/audit/stats');
  },
};
