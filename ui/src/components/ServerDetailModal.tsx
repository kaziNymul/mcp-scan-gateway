'use client';

import { useState } from 'react';
import {
  X,
  Shield,
  ShieldX,
  AlertTriangle,
  CheckCircle,
  XCircle,
  RefreshCw,
  Trash2,
  Clock,
  User,
  FileText,
} from 'lucide-react';
import { registryApi } from '@/lib/api';
import { ServerRegistration, ScanResult } from '@/types/registry';
import { getStatusColor, formatDate, getRiskColor } from '@/lib/utils';
import useSWR from 'swr';

interface ServerDetailModalProps {
  server: ServerRegistration;
  isAdmin: boolean;
  onClose: () => void;
  onUpdate: () => void;
}

export function ServerDetailModal({
  server,
  isAdmin,
  onClose,
  onUpdate,
}: ServerDetailModalProps) {
  const [activeTab, setActiveTab] = useState<'details' | 'scans' | 'approvals'>(
    'details'
  );
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [justification, setJustification] = useState('');
  const [showApprovalForm, setShowApprovalForm] = useState<
    'approve' | 'deny' | null
  >(null);

  const { data: scans } = useSWR<ScanResult[]>(
    activeTab === 'scans' ? ['scans', server.id] : null,
    () => registryApi.getScans(server.id)
  );

  const handleAction = async (action: 'approve' | 'deny' | 'suspend' | 'scan') => {
    setError(null);
    setIsLoading(true);

    try {
      switch (action) {
        case 'approve':
          await registryApi.approveServer(server.id, justification);
          break;
        case 'deny':
          await registryApi.denyServer(server.id, justification);
          break;
        case 'suspend':
          await registryApi.suspendServer(server.id, justification);
          break;
        case 'scan':
          await registryApi.triggerScan(server.id);
          break;
      }
      setShowApprovalForm(null);
      setJustification('');
      onUpdate();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Action failed');
    } finally {
      setIsLoading(false);
    }
  };

  const canApprove =
    isAdmin &&
    ['ScannedPass', 'ScannedFail', 'PendingApproval'].includes(server.status);
  const canDeny =
    isAdmin &&
    ['ScannedPass', 'ScannedFail', 'PendingApproval', 'Approved'].includes(
      server.status
    );
  const canSuspend = isAdmin && server.status === 'Approved';
  const canScan = ['Draft', 'ScannedPass', 'ScannedFail'].includes(server.status);

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-xl shadow-xl max-w-3xl w-full max-h-[90vh] overflow-hidden flex flex-col">
        {/* Header */}
        <div className="flex items-center justify-between p-4 border-b">
          <div>
            <h2 className="text-lg font-semibold">{server.name}</h2>
            <p className="text-sm text-gray-500 font-mono">{server.canonicalId}</p>
          </div>
          <button
            onClick={onClose}
            className="p-2 hover:bg-gray-100 rounded-lg"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Status bar */}
        <div className="flex items-center gap-4 px-4 py-3 bg-gray-50 border-b">
          <div className="flex items-center gap-2">
            <span className="text-sm text-gray-600">Status:</span>
            <span
              className={`px-2 py-1 text-xs font-medium rounded-full ${getStatusColor(
                server.status
              )}`}
            >
              {server.status}
            </span>
          </div>
          {server.latestRiskScore !== undefined && (
            <div className="flex items-center gap-2">
              <span className="text-sm text-gray-600">Risk Score:</span>
              <span
                className={`px-2 py-1 text-xs font-medium rounded-full ${getRiskColor(
                  server.latestRiskScore
                )}`}
              >
                {server.latestRiskScore.toFixed(0)}
              </span>
            </div>
          )}
          <div className="flex items-center gap-2 text-sm text-gray-600">
            <User className="w-4 h-4" />
            {server.ownerTeam}
          </div>
        </div>

        {/* Error message */}
        {error && (
          <div className="mx-4 mt-4 p-3 bg-red-50 border border-red-200 rounded-lg text-red-800 text-sm">
            {error}
          </div>
        )}

        {/* Tabs */}
        <div className="flex border-b">
          {(['details', 'scans', 'approvals'] as const).map((tab) => (
            <button
              key={tab}
              onClick={() => setActiveTab(tab)}
              className={`px-4 py-3 text-sm font-medium border-b-2 -mb-px ${
                activeTab === tab
                  ? 'border-blue-600 text-blue-600'
                  : 'border-transparent text-gray-500 hover:text-gray-700'
              }`}
            >
              {tab.charAt(0).toUpperCase() + tab.slice(1)}
            </button>
          ))}
        </div>

        {/* Tab content */}
        <div className="flex-1 overflow-y-auto p-4">
          {activeTab === 'details' && (
            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="text-xs text-gray-500 uppercase">
                    Source Type
                  </label>
                  <p className="font-medium">{server.sourceType}</p>
                </div>
                <div>
                  <label className="text-xs text-gray-500 uppercase">
                    Created By
                  </label>
                  <p className="font-medium">{server.createdBy}</p>
                </div>
                <div>
                  <label className="text-xs text-gray-500 uppercase">
                    Created At
                  </label>
                  <p className="font-medium">{formatDate(server.createdAt)}</p>
                </div>
                <div>
                  <label className="text-xs text-gray-500 uppercase">
                    Updated At
                  </label>
                  <p className="font-medium">{formatDate(server.updatedAt)}</p>
                </div>
              </div>

              <div>
                <label className="text-xs text-gray-500 uppercase">
                  Declared Tools
                </label>
                <div className="flex flex-wrap gap-2 mt-1">
                  {server.declaredTools.length > 0 ? (
                    server.declaredTools.map((tool) => (
                      <span
                        key={tool}
                        className="px-2 py-1 bg-gray-100 rounded text-sm font-mono"
                      >
                        {tool}
                      </span>
                    ))
                  ) : (
                    <span className="text-gray-400">No tools declared</span>
                  )}
                </div>
              </div>

              <div>
                <label className="text-xs text-gray-500 uppercase">
                  MCP Configuration
                </label>
                <pre className="mt-1 p-3 bg-gray-100 rounded-lg text-xs overflow-x-auto">
                  {JSON.stringify(server.mcpConfig, null, 2)}
                </pre>
              </div>
            </div>
          )}

          {activeTab === 'scans' && (
            <div className="space-y-4">
              {scans && scans.length > 0 ? (
                scans.map((scan) => (
                  <div
                    key={scan.id}
                    className="border rounded-lg p-4 space-y-3"
                  >
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-3">
                        <span
                          className={`px-2 py-1 text-xs font-medium rounded-full ${getRiskColor(
                            scan.riskScore
                          )}`}
                        >
                          Risk: {scan.riskScore.toFixed(0)}
                        </span>
                        <span className="text-sm text-gray-500">
                          {formatDate(scan.startedAt)}
                        </span>
                      </div>
                      <span
                        className={`text-xs font-medium ${
                          scan.status === 'Completed'
                            ? 'text-green-600'
                            : 'text-yellow-600'
                        }`}
                      >
                        {scan.status}
                      </span>
                    </div>

                    {scan.issues.length > 0 && (
                      <div>
                        <p className="text-sm font-medium text-gray-700 mb-2">
                          Issues ({scan.issues.length})
                        </p>
                        <div className="space-y-2">
                          {scan.issues.slice(0, 5).map((issue, idx) => (
                            <div
                              key={idx}
                              className={`flex items-start gap-2 text-sm p-2 rounded ${
                                issue.severity === 'critical' ||
                                issue.severity === 'high'
                                  ? 'bg-red-50 text-red-800'
                                  : issue.severity === 'medium'
                                  ? 'bg-yellow-50 text-yellow-800'
                                  : 'bg-gray-50 text-gray-700'
                              }`}
                            >
                              <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5" />
                              <div>
                                <span className="font-medium uppercase text-xs">
                                  [{issue.severity}]
                                </span>{' '}
                                {issue.message}
                              </div>
                            </div>
                          ))}
                        </div>
                      </div>
                    )}

                    {scan.discoveredTools.length > 0 && (
                      <div>
                        <p className="text-sm font-medium text-gray-700 mb-2">
                          Discovered Tools ({scan.discoveredTools.length})
                        </p>
                        <div className="flex flex-wrap gap-2">
                          {scan.discoveredTools.map((tool) => (
                            <span
                              key={tool.name}
                              className="px-2 py-1 bg-blue-50 text-blue-700 rounded text-sm"
                            >
                              {tool.name}
                            </span>
                          ))}
                        </div>
                      </div>
                    )}
                  </div>
                ))
              ) : (
                <div className="text-center py-8 text-gray-500">
                  <FileText className="w-8 h-8 mx-auto mb-2 opacity-50" />
                  <p>No scan results yet</p>
                </div>
              )}
            </div>
          )}

          {activeTab === 'approvals' && (
            <div className="text-center py-8 text-gray-500">
              <Clock className="w-8 h-8 mx-auto mb-2 opacity-50" />
              <p>Approval history will appear here</p>
            </div>
          )}
        </div>

        {/* Approval form */}
        {showApprovalForm && (
          <div className="p-4 border-t bg-gray-50">
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Justification for {showApprovalForm === 'approve' ? 'approval' : 'denial'}
            </label>
            <textarea
              value={justification}
              onChange={(e) => setJustification(e.target.value)}
              rows={2}
              className="w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="Enter reason for this decision..."
            />
            <div className="flex justify-end gap-2 mt-3">
              <button
                onClick={() => setShowApprovalForm(null)}
                className="px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-200 rounded"
              >
                Cancel
              </button>
              <button
                onClick={() => handleAction(showApprovalForm)}
                disabled={!justification || isLoading}
                className={`px-3 py-1.5 text-sm text-white rounded disabled:opacity-50 ${
                  showApprovalForm === 'approve'
                    ? 'bg-green-600 hover:bg-green-700'
                    : 'bg-red-600 hover:bg-red-700'
                }`}
              >
                {isLoading
                  ? 'Processing...'
                  : showApprovalForm === 'approve'
                  ? 'Confirm Approval'
                  : 'Confirm Denial'}
              </button>
            </div>
          </div>
        )}

        {/* Actions footer */}
        <div className="flex items-center justify-between p-4 border-t bg-gray-50">
          <div className="flex gap-2">
            {canScan && (
              <button
                onClick={() => handleAction('scan')}
                disabled={isLoading}
                className="flex items-center gap-1.5 px-3 py-1.5 text-sm border rounded-lg hover:bg-gray-100"
              >
                <RefreshCw className="w-4 h-4" />
                {server.sourceType === 'LocalDeclared' ? 'Upload Scan' : 'Trigger Scan'}
              </button>
            )}
          </div>

          <div className="flex gap-2">
            {canSuspend && (
              <button
                onClick={() => handleAction('suspend')}
                disabled={isLoading}
                className="flex items-center gap-1.5 px-3 py-1.5 text-sm border border-orange-300 text-orange-700 rounded-lg hover:bg-orange-50"
              >
                <ShieldX className="w-4 h-4" />
                Suspend
              </button>
            )}
            {canDeny && (
              <button
                onClick={() => setShowApprovalForm('deny')}
                disabled={isLoading}
                className="flex items-center gap-1.5 px-3 py-1.5 text-sm bg-red-600 text-white rounded-lg hover:bg-red-700"
              >
                <XCircle className="w-4 h-4" />
                Deny
              </button>
            )}
            {canApprove && (
              <button
                onClick={() => setShowApprovalForm('approve')}
                disabled={isLoading}
                className="flex items-center gap-1.5 px-3 py-1.5 text-sm bg-green-600 text-white rounded-lg hover:bg-green-700"
              >
                <CheckCircle className="w-4 h-4" />
                Approve
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
