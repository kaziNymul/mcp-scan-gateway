'use client';

import { useSession } from 'next-auth/react';
import { redirect } from 'next/navigation';
import useSWR from 'swr';
import { CheckCircle, XCircle, AlertTriangle } from 'lucide-react';
import { registryApi } from '@/lib/api';
import { ServerRegistration } from '@/types/registry';
import { getStatusColor, formatDate, getRiskColor } from '@/lib/utils';
import { useState } from 'react';
import { ServerDetailModal } from '@/components/ServerDetailModal';

export default function ApprovalsPage() {
  const { data: session, status } = useSession();
  const [selectedServer, setSelectedServer] = useState<ServerRegistration | null>(
    null
  );

  // Redirect non-admins
  if (status === 'authenticated' && !session?.user?.isAdmin) {
    redirect('/dashboard');
  }

  const {
    data: servers,
    error,
    isLoading,
    mutate,
  } = useSWR<ServerRegistration[]>(
    ['servers', 'pending'],
    () =>
      registryApi.listServers().then((all) =>
        all.filter((s) =>
          ['ScannedPass', 'ScannedFail', 'PendingApproval'].includes(s.status)
        )
      ),
    { refreshInterval: 10000 }
  );

  const isAdmin = session?.user?.isAdmin ?? false;

  // Group by status
  const scannedPass = servers?.filter((s) => s.status === 'ScannedPass') || [];
  const scannedFail = servers?.filter((s) => s.status === 'ScannedFail') || [];
  const pending = servers?.filter((s) => s.status === 'PendingApproval') || [];

  return (
    <div>
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Pending Approvals</h1>
        <p className="text-gray-600 mt-1">
          Review and approve MCP servers awaiting governance decisions
        </p>
      </div>

      {/* Error state */}
      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-4 mb-6">
          <p className="text-red-800">Failed to load servers: {error.message}</p>
        </div>
      )}

      {/* Loading state */}
      {isLoading && (
        <div className="flex items-center justify-center py-12">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
        </div>
      )}

      {/* Stats */}
      {servers && (
        <div className="grid grid-cols-3 gap-4 mb-6">
          <div className="bg-white rounded-lg border p-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 bg-green-100 rounded-lg flex items-center justify-center">
                <CheckCircle className="w-5 h-5 text-green-600" />
              </div>
              <div>
                <p className="text-2xl font-bold">{scannedPass.length}</p>
                <p className="text-sm text-gray-500">Passed Scan</p>
              </div>
            </div>
          </div>
          <div className="bg-white rounded-lg border p-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 bg-orange-100 rounded-lg flex items-center justify-center">
                <AlertTriangle className="w-5 h-5 text-orange-600" />
              </div>
              <div>
                <p className="text-2xl font-bold">{scannedFail.length}</p>
                <p className="text-sm text-gray-500">Failed Scan</p>
              </div>
            </div>
          </div>
          <div className="bg-white rounded-lg border p-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 bg-yellow-100 rounded-lg flex items-center justify-center">
                <XCircle className="w-5 h-5 text-yellow-600" />
              </div>
              <div>
                <p className="text-2xl font-bold">{pending.length}</p>
                <p className="text-sm text-gray-500">Pending Review</p>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Failed scans first (need attention) */}
      {scannedFail.length > 0 && (
        <div className="mb-6">
          <h2 className="text-lg font-semibold text-gray-900 mb-3 flex items-center gap-2">
            <AlertTriangle className="w-5 h-5 text-orange-600" />
            Failed Scan - Needs Review
          </h2>
          <div className="bg-white rounded-lg shadow overflow-hidden">
            <ServerTable
              servers={scannedFail}
              onSelect={setSelectedServer}
            />
          </div>
        </div>
      )}

      {/* Passed scans */}
      {scannedPass.length > 0 && (
        <div className="mb-6">
          <h2 className="text-lg font-semibold text-gray-900 mb-3 flex items-center gap-2">
            <CheckCircle className="w-5 h-5 text-green-600" />
            Passed Scan - Ready for Approval
          </h2>
          <div className="bg-white rounded-lg shadow overflow-hidden">
            <ServerTable
              servers={scannedPass}
              onSelect={setSelectedServer}
            />
          </div>
        </div>
      )}

      {/* Empty state */}
      {servers && servers.length === 0 && (
        <div className="text-center py-12 bg-white rounded-lg border-2 border-dashed">
          <div className="mx-auto w-12 h-12 bg-green-100 rounded-full flex items-center justify-center mb-4">
            <CheckCircle className="w-6 h-6 text-green-600" />
          </div>
          <h3 className="text-lg font-medium text-gray-900 mb-1">
            All caught up!
          </h3>
          <p className="text-gray-500">
            No servers are waiting for approval
          </p>
        </div>
      )}

      {/* Detail modal */}
      {selectedServer && (
        <ServerDetailModal
          server={selectedServer}
          isAdmin={isAdmin}
          onClose={() => setSelectedServer(null)}
          onUpdate={() => {
            setSelectedServer(null);
            mutate();
          }}
        />
      )}
    </div>
  );
}

function ServerTable({
  servers,
  onSelect,
}: {
  servers: ServerRegistration[];
  onSelect: (s: ServerRegistration) => void;
}) {
  return (
    <table className="min-w-full divide-y divide-gray-200">
      <thead className="bg-gray-50">
        <tr>
          <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">
            Server
          </th>
          <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">
            Owner
          </th>
          <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">
            Risk Score
          </th>
          <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">
            Scanned
          </th>
          <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">
            Action
          </th>
        </tr>
      </thead>
      <tbody className="bg-white divide-y divide-gray-200">
        {servers.map((server) => (
          <tr
            key={server.id}
            className="hover:bg-gray-50 cursor-pointer"
            onClick={() => onSelect(server)}
          >
            <td className="px-6 py-4">
              <div>
                <p className="font-medium text-gray-900">{server.name}</p>
                <p className="text-sm text-gray-500 font-mono">
                  {server.canonicalId}
                </p>
              </div>
            </td>
            <td className="px-6 py-4 text-sm text-gray-600">
              {server.ownerTeam}
            </td>
            <td className="px-6 py-4">
              {server.latestRiskScore !== undefined ? (
                <span
                  className={`inline-flex px-2 py-1 text-xs font-medium rounded-full ${getRiskColor(
                    server.latestRiskScore
                  )}`}
                >
                  {server.latestRiskScore.toFixed(0)}
                </span>
              ) : (
                <span className="text-gray-400">—</span>
              )}
            </td>
            <td className="px-6 py-4 text-sm text-gray-500">
              {formatDate(server.updatedAt)}
            </td>
            <td className="px-6 py-4 text-right">
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  onSelect(server);
                }}
                className="text-blue-600 hover:text-blue-800 text-sm font-medium"
              >
                Review
              </button>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
