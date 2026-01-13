'use client';

import { useState } from 'react';
import useSWR from 'swr';
import { Search, Filter, Download, CheckCircle, XCircle } from 'lucide-react';
import { auditApi } from '@/lib/api';
import { AuditEvent } from '@/types/registry';
import { formatDate } from '@/lib/utils';

export default function AuditPage() {
  const [filters, setFilters] = useState({
    serverCanonicalId: '',
    actorId: '',
    decision: '',
    limit: 100,
  });

  const {
    data: events,
    error,
    isLoading,
  } = useSWR<AuditEvent[]>(
    ['audit', filters],
    () =>
      auditApi.queryEvents({
        ...filters,
        serverCanonicalId: filters.serverCanonicalId || undefined,
        actorId: filters.actorId || undefined,
        decision: filters.decision || undefined,
      }),
    { refreshInterval: 30000 }
  );

  const { data: stats } = useSWR('audit-stats', () => auditApi.getStats());

  const handleExport = () => {
    if (!events) return;
    const csv = [
      ['Timestamp', 'Actor', 'Server', 'Tool', 'Decision', 'Reason', 'Latency (ms)'],
      ...events.map((e) => [
        e.timestamp,
        e.actorEmail || e.actorId,
        e.serverCanonicalId,
        e.toolName,
        e.decision,
        e.denyReason || '',
        e.latencyMs.toString(),
      ]),
    ]
      .map((row) => row.join(','))
      .join('\n');

    const blob = new Blob([csv], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `audit-log-${new Date().toISOString().split('T')[0]}.csv`;
    a.click();
  };

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Audit Logs</h1>
          <p className="text-gray-600 mt-1">
            View all tool invocations and governance decisions
          </p>
        </div>
        <button
          onClick={handleExport}
          disabled={!events || events.length === 0}
          className="flex items-center gap-2 px-4 py-2 border rounded-lg hover:bg-gray-50 disabled:opacity-50"
        >
          <Download className="w-4 h-4" />
          Export CSV
        </button>
      </div>

      {/* Stats */}
      {stats && (
        <div className="grid grid-cols-4 gap-4 mb-6">
          <div className="bg-white rounded-lg border p-4">
            <p className="text-2xl font-bold">{stats.totalEvents.toLocaleString()}</p>
            <p className="text-sm text-gray-500">Total Events</p>
          </div>
          <div className="bg-white rounded-lg border p-4">
            <div className="flex items-center gap-2">
              <CheckCircle className="w-5 h-5 text-green-600" />
              <p className="text-2xl font-bold text-green-600">
                {stats.allowedCount.toLocaleString()}
              </p>
            </div>
            <p className="text-sm text-gray-500">Allowed</p>
          </div>
          <div className="bg-white rounded-lg border p-4">
            <div className="flex items-center gap-2">
              <XCircle className="w-5 h-5 text-red-600" />
              <p className="text-2xl font-bold text-red-600">
                {stats.deniedCount.toLocaleString()}
              </p>
            </div>
            <p className="text-sm text-gray-500">Denied</p>
          </div>
          <div className="bg-white rounded-lg border p-4">
            <p className="text-2xl font-bold">
              {stats.totalEvents > 0
                ? ((stats.allowedCount / stats.totalEvents) * 100).toFixed(1)
                : 0}
              %
            </p>
            <p className="text-sm text-gray-500">Success Rate</p>
          </div>
        </div>
      )}

      {/* Filters */}
      <div className="flex flex-wrap gap-4 mb-6">
        <div className="flex-1 min-w-[200px]">
          <label className="block text-xs text-gray-500 mb-1">Server</label>
          <input
            type="text"
            placeholder="Filter by server..."
            value={filters.serverCanonicalId}
            onChange={(e) =>
              setFilters({ ...filters, serverCanonicalId: e.target.value })
            }
            className="w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500"
          />
        </div>
        <div className="flex-1 min-w-[200px]">
          <label className="block text-xs text-gray-500 mb-1">Actor</label>
          <input
            type="text"
            placeholder="Filter by actor..."
            value={filters.actorId}
            onChange={(e) => setFilters({ ...filters, actorId: e.target.value })}
            className="w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500"
          />
        </div>
        <div className="min-w-[150px]">
          <label className="block text-xs text-gray-500 mb-1">Decision</label>
          <select
            value={filters.decision}
            onChange={(e) => setFilters({ ...filters, decision: e.target.value })}
            className="w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500"
          >
            <option value="">All</option>
            <option value="Allowed">Allowed</option>
            <option value="DeniedServerNotApproved">Denied - Not Approved</option>
            <option value="DeniedToolDenylisted">Denied - Tool Blocked</option>
            <option value="DeniedHighRisk">Denied - High Risk</option>
          </select>
        </div>
      </div>

      {/* Error state */}
      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-4 mb-6">
          <p className="text-red-800">Failed to load audit logs: {error.message}</p>
        </div>
      )}

      {/* Loading state */}
      {isLoading && (
        <div className="flex items-center justify-center py-12">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
        </div>
      )}

      {/* Events table */}
      {events && events.length > 0 && (
        <div className="bg-white rounded-lg shadow overflow-hidden">
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                    Time
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                    Actor
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                    Server
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                    Tool
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                    Decision
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                    Latency
                  </th>
                </tr>
              </thead>
              <tbody className="bg-white divide-y divide-gray-200">
                {events.map((event) => (
                  <tr key={event.id} className="hover:bg-gray-50">
                    <td className="px-4 py-3 text-sm text-gray-500 whitespace-nowrap">
                      {formatDate(event.timestamp)}
                    </td>
                    <td className="px-4 py-3 text-sm">
                      <div>
                        <p className="text-gray-900">
                          {event.actorEmail || event.actorId}
                        </p>
                        {event.team && (
                          <p className="text-xs text-gray-500">{event.team}</p>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3 text-sm font-mono text-gray-600 max-w-[200px] truncate">
                      {event.serverCanonicalId}
                    </td>
                    <td className="px-4 py-3 text-sm font-mono text-gray-600">
                      {event.toolName}
                    </td>
                    <td className="px-4 py-3">
                      <div>
                        <span
                          className={`inline-flex px-2 py-0.5 text-xs font-medium rounded-full ${
                            event.decision === 'Allowed'
                              ? 'bg-green-100 text-green-800'
                              : 'bg-red-100 text-red-800'
                          }`}
                        >
                          {event.decision === 'Allowed' ? 'Allowed' : 'Denied'}
                        </span>
                        {event.denyReason && (
                          <p className="text-xs text-red-600 mt-0.5 max-w-[200px] truncate">
                            {event.denyReason}
                          </p>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-500">
                      {event.latencyMs}ms
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Empty state */}
      {events && events.length === 0 && (
        <div className="text-center py-12 bg-white rounded-lg border-2 border-dashed">
          <p className="text-gray-500">No audit events found</p>
        </div>
      )}
    </div>
  );
}
