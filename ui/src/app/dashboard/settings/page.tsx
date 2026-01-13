'use client';

import { useSession } from 'next-auth/react';
import { redirect } from 'next/navigation';
import { useState } from 'react';
import { Save, AlertTriangle } from 'lucide-react';

export default function SettingsPage() {
  const { data: session, status } = useSession();

  // Redirect non-admins
  if (status === 'authenticated' && !session?.user?.isAdmin) {
    redirect('/dashboard');
  }

  const [isSaving, setIsSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  const [policy, setPolicy] = useState({
    globalToolDenylist: 'shell_execute, arbitrary_code_run',
    riskThreshold: 50,
    scanPassThreshold: 30,
    requireAdminForHighRisk: true,
    enforceRegistryOnly: true,
    enforcementMode: 'Enforce',
  });

  const handleSave = async () => {
    setIsSaving(true);
    // TODO: Implement policy update API call
    await new Promise((resolve) => setTimeout(resolve, 1000));
    setIsSaving(false);
    setSaved(true);
    setTimeout(() => setSaved(false), 3000);
  };

  return (
    <div className="max-w-3xl">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Settings</h1>
        <p className="text-gray-600 mt-1">
          Configure governance policies and enforcement rules
        </p>
      </div>

      <div className="bg-white rounded-lg shadow">
        <div className="p-6 border-b">
          <h2 className="text-lg font-semibold">Enforcement Mode</h2>
          <p className="text-sm text-gray-500 mt-1">
            Control how policy violations are handled
          </p>

          <div className="mt-4 space-y-3">
            {[
              {
                value: 'Enforce',
                label: 'Enforce',
                desc: 'Block non-compliant requests',
              },
              {
                value: 'AuditOnly',
                label: 'Audit Only',
                desc: 'Log violations but allow requests',
              },
              {
                value: 'Disabled',
                label: 'Disabled',
                desc: 'No policy checks (not recommended)',
              },
            ].map((option) => (
              <label
                key={option.value}
                className={`flex items-center gap-3 p-3 border rounded-lg cursor-pointer ${
                  policy.enforcementMode === option.value
                    ? 'border-blue-500 bg-blue-50'
                    : 'hover:bg-gray-50'
                }`}
              >
                <input
                  type="radio"
                  name="enforcementMode"
                  value={option.value}
                  checked={policy.enforcementMode === option.value}
                  onChange={(e) =>
                    setPolicy({ ...policy, enforcementMode: e.target.value })
                  }
                  className="text-blue-600"
                />
                <div>
                  <p className="font-medium">{option.label}</p>
                  <p className="text-sm text-gray-500">{option.desc}</p>
                </div>
              </label>
            ))}
          </div>

          {policy.enforcementMode === 'Disabled' && (
            <div className="mt-4 p-3 bg-yellow-50 border border-yellow-200 rounded-lg flex items-start gap-2">
              <AlertTriangle className="w-5 h-5 text-yellow-600 shrink-0 mt-0.5" />
              <p className="text-sm text-yellow-800">
                Disabling enforcement removes all governance protections. Only use
                for emergency troubleshooting.
              </p>
            </div>
          )}
        </div>

        <div className="p-6 border-b">
          <h2 className="text-lg font-semibold">Risk Thresholds</h2>
          <p className="text-sm text-gray-500 mt-1">
            Configure scan score thresholds for approvals
          </p>

          <div className="mt-4 grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Risk Threshold (Block above)
              </label>
              <input
                type="number"
                min={0}
                max={100}
                value={policy.riskThreshold}
                onChange={(e) =>
                  setPolicy({ ...policy, riskThreshold: parseInt(e.target.value) })
                }
                className="w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500"
              />
              <p className="text-xs text-gray-500 mt-1">
                Servers with scores above this require admin approval
              </p>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Auto-Pass Threshold
              </label>
              <input
                type="number"
                min={0}
                max={100}
                value={policy.scanPassThreshold}
                onChange={(e) =>
                  setPolicy({
                    ...policy,
                    scanPassThreshold: parseInt(e.target.value),
                  })
                }
                className="w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500"
              />
              <p className="text-xs text-gray-500 mt-1">
                Servers below this score are marked as passed
              </p>
            </div>
          </div>
        </div>

        <div className="p-6 border-b">
          <h2 className="text-lg font-semibold">Tool Denylist</h2>
          <p className="text-sm text-gray-500 mt-1">
            Tools that are blocked globally for all users
          </p>

          <div className="mt-4">
            <textarea
              value={policy.globalToolDenylist}
              onChange={(e) =>
                setPolicy({ ...policy, globalToolDenylist: e.target.value })
              }
              rows={3}
              className="w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 font-mono text-sm"
              placeholder="shell_execute, arbitrary_code_run, ..."
            />
            <p className="text-xs text-gray-500 mt-1">
              Comma-separated list of tool names to block
            </p>
          </div>
        </div>

        <div className="p-6 border-b">
          <h2 className="text-lg font-semibold">Access Controls</h2>

          <div className="mt-4 space-y-3">
            <label className="flex items-center gap-3">
              <input
                type="checkbox"
                checked={policy.enforceRegistryOnly}
                onChange={(e) =>
                  setPolicy({ ...policy, enforceRegistryOnly: e.target.checked })
                }
                className="w-4 h-4 text-blue-600 rounded"
              />
              <div>
                <p className="font-medium">Enforce Registry Only</p>
                <p className="text-sm text-gray-500">
                  Block access to unregistered servers
                </p>
              </div>
            </label>

            <label className="flex items-center gap-3">
              <input
                type="checkbox"
                checked={policy.requireAdminForHighRisk}
                onChange={(e) =>
                  setPolicy({
                    ...policy,
                    requireAdminForHighRisk: e.target.checked,
                  })
                }
                className="w-4 h-4 text-blue-600 rounded"
              />
              <div>
                <p className="font-medium">Require Admin for High-Risk</p>
                <p className="text-sm text-gray-500">
                  Only admins can approve servers above risk threshold
                </p>
              </div>
            </label>
          </div>
        </div>

        <div className="p-6 flex items-center justify-between">
          {saved && (
            <p className="text-green-600 text-sm">Settings saved successfully!</p>
          )}
          <div className="flex-1" />
          <button
            onClick={handleSave}
            disabled={isSaving}
            className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50"
          >
            <Save className="w-4 h-4" />
            {isSaving ? 'Saving...' : 'Save Settings'}
          </button>
        </div>
      </div>
    </div>
  );
}
