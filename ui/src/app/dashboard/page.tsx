'use client';

import { useSession } from 'next-auth/react';
import { useState } from 'react';
import useSWR from 'swr';
import { Plus, Search, Filter, RefreshCw } from 'lucide-react';
import { registryApi } from '@/lib/api';
import { ServerRegistration } from '@/types/registry';
import { getStatusColor, formatDate, getRiskColor } from '@/lib/utils';
import { RegisterServerModal } from '@/components/RegisterServerModal';
import { ServerDetailModal } from '@/components/ServerDetailModal';

export default function ServersPage() {
  const { data: session } = useSession();
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [showRegisterModal, setShowRegisterModal] = useState(false);
  const [selectedServer, setSelectedServer] = useState<ServerRegistration | null>(null);

  const {
    data: servers,
    error,
    isLoading,
    mutate,
  } = useSWR<ServerRegistration[]>(
    ['servers', statusFilter],
    () => registryApi.listServers({ status: statusFilter || undefined }),
    { refreshInterval: 10000 }
  );

  const filteredServers = servers?.filter(
    (server) =>
      server.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
      server.canonicalId.toLowerCase().includes(searchQuery.toLowerCase()) ||
      server.ownerTeam.toLowerCase().includes(searchQuery.toLowerCase())
  );

  const isAdmin = session?.user?.isAdmin ?? false;

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">MCP Servers</h1>
          <p className="text-gray-600 mt-1">
            Manage registered MCP servers in the governance registry
          </p>
        </div>
        <button
          onClick={() => setShowRegisterModal(true)}
          className="flex items-center gap-2 bg-blue-600 text-white px-4 py-2 rounded-lg hover:bg-blue-700 transition-colors"
        >
          <Plus className="w-4 h-4" />
          Register Server
        </button>
      </div>

      {/* Filters */}
      <div className="flex flex-col sm:flex-row gap-4 mb-6">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
          <input
            type="text"
            placeholder="Search servers..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="w-full pl-10 pr-4 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
          />
        </div>
        <div className="flex items-center gap-2">
          <Filter className="w-4 h-4 text-gray-400" />
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            className="border rounded-lg px-3 py-2 focus:ring-2 focus:ring-blue-500"
          >
            <option value="">All Status</option>
            <option value="Draft">Draft</option>
            <option value="PendingScan">Pending Scan</option>
            <option value="Scanning">Scanning</option>
            <option value="ScannedPass">Scanned (Pass)</option>
            <option value="ScannedFail">Scanned (Fail)</option>
            <option value="PendingApproval">Pending Approval</option>
            <option value="Approved">Approved</option>
            <option value="Denied">Denied</option>
            <option value="Suspended">Suspended</option>
          </select>
          <button
            onClick={() => mutate()}
            className="p-2 border rounded-lg hover:bg-gray-50"
            title="Refresh"
          >
            <RefreshCw className="w-4 h-4" />
          </button>
        </div>
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

      {/* Server list */}
      {filteredServers && filteredServers.length > 0 && (
        <div className="bg-white rounded-lg shadow overflow-hidden">
          <table className="min-w-full divide-y divide-gray-200">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Server
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Owner
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Status
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Risk Score
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Updated
                </th>
                <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Actions
                </th>
              </tr>
            </thead>
            <tbody className="bg-white divide-y divide-gray-200">
              {filteredServers.map((server) => (
                <tr
                  key={server.id}
                  className="hover:bg-gray-50 cursor-pointer"
                  onClick={() => setSelectedServer(server)}
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
                    <span
                      className={`inline-flex px-2 py-1 text-xs font-medium rounded-full ${getStatusColor(
                        server.status
                      )}`}
                    >
                      {server.status}
                    </span>
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
                        setSelectedServer(server);
                      }}
                      className="text-blue-600 hover:text-blue-800 text-sm font-medium"
                    >
                      View
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Empty state */}
      {filteredServers && filteredServers.length === 0 && (
        <div className="text-center py-12 bg-white rounded-lg border-2 border-dashed">
          <div className="mx-auto w-12 h-12 bg-gray-100 rounded-full flex items-center justify-center mb-4">
            <Server className="w-6 h-6 text-gray-400" />
          </div>
          <h3 className="text-lg font-medium text-gray-900 mb-1">
            No servers found
          </h3>
          <p className="text-gray-500 mb-4">
            {searchQuery || statusFilter
              ? 'Try adjusting your filters'
              : 'Get started by registering your first MCP server'}
          </p>
          {!searchQuery && !statusFilter && (
            <button
              onClick={() => setShowRegisterModal(true)}
              className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2 rounded-lg hover:bg-blue-700"
            >
              <Plus className="w-4 h-4" />
              Register Server
            </button>
          )}
        </div>
      )}

      {/* Modals */}
      {showRegisterModal && (
        <RegisterServerModal
          onClose={() => setShowRegisterModal(false)}
          onSuccess={() => {
            setShowRegisterModal(false);
            mutate();
          }}
        />
      )}

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
