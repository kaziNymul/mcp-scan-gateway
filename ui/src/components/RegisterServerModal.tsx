'use client';

import { useState } from 'react';
import { X, Upload, Terminal } from 'lucide-react';
import { registryApi } from '@/lib/api';
import { ServerRegistrationRequest, SourceType } from '@/types/registry';

interface RegisterServerModalProps {
  onClose: () => void;
  onSuccess: () => void;
}

export function RegisterServerModal({
  onClose,
  onSuccess,
}: RegisterServerModalProps) {
  const [step, setStep] = useState<'form' | 'scan'>('form');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [serverId, setServerId] = useState<string | null>(null);

  const [formData, setFormData] = useState<ServerRegistrationRequest>({
    canonicalId: '',
    name: '',
    description: '',
    ownerTeam: '',
    sourceType: 'LocalDeclared',
    declaredTools: [],
    mcpConfig: {
      transport: 'sse',
      url: '',
    },
  });

  const [toolsInput, setToolsInput] = useState('');
  const [scanOutput, setScanOutput] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      const request: ServerRegistrationRequest = {
        ...formData,
        declaredTools: toolsInput
          .split(',')
          .map((t) => t.trim())
          .filter(Boolean),
      };

      const result = await registryApi.registerServer(request);
      setServerId(result.id);

      if (formData.sourceType === 'LocalDeclared') {
        setStep('scan');
      } else {
        // For container/repo types, trigger remote scan
        await registryApi.triggerScan(result.id);
        onSuccess();
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to register server');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleScanUpload = async () => {
    if (!serverId || !scanOutput) return;

    setError(null);
    setIsSubmitting(true);

    try {
      await registryApi.uploadLocalScan({
        serverId,
        scanOutput,
        scanVersion: '1.0.0',
        scannedAt: new Date().toISOString(),
      });
      onSuccess();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to upload scan results');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-xl shadow-xl max-w-2xl w-full max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between p-4 border-b">
          <h2 className="text-lg font-semibold">
            {step === 'form' ? 'Register MCP Server' : 'Upload Scan Results'}
          </h2>
          <button
            onClick={onClose}
            className="p-2 hover:bg-gray-100 rounded-lg"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {error && (
          <div className="mx-4 mt-4 p-3 bg-red-50 border border-red-200 rounded-lg text-red-800 text-sm">
            {error}
          </div>
        )}

        {step === 'form' ? (
          <form onSubmit={handleSubmit} className="p-4 space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Server Name *
                </label>
                <input
                  type="text"
                  required
                  value={formData.name}
                  onChange={(e) =>
                    setFormData({ ...formData, name: e.target.value })
                  }
                  className="w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500"
                  placeholder="My MCP Server"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Owner Team *
                </label>
                <input
                  type="text"
                  required
                  value={formData.ownerTeam}
                  onChange={(e) =>
                    setFormData({ ...formData, ownerTeam: e.target.value })
                  }
                  className="w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500"
                  placeholder="platform-team"
                />
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Canonical ID *
              </label>
              <input
                type="text"
                required
                value={formData.canonicalId}
                onChange={(e) =>
                  setFormData({ ...formData, canonicalId: e.target.value })
                }
                className="w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 font-mono text-sm"
                placeholder="github.com/org/my-mcp-server or local/my-server"
              />
              <p className="text-xs text-gray-500 mt-1">
                Unique identifier for this server (e.g., repo URL, container image)
              </p>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Source Type *
              </label>
              <select
                value={formData.sourceType}
                onChange={(e) =>
                  setFormData({
                    ...formData,
                    sourceType: e.target.value as SourceType,
                  })
                }
                className="w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500"
              >
                <option value="LocalDeclared">Local (Developer Machine)</option>
                <option value="ContainerImage">Container Image</option>
                <option value="ExternalRepo">External Repository</option>
                <option value="InternalRepo">Internal Repository</option>
                <option value="PackageArtifact">Package Artifact</option>
              </select>
            </div>

            {formData.sourceType === 'LocalDeclared' && (
              <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-3">
                <p className="text-sm text-yellow-800">
                  <strong>Local Server:</strong> You'll need to run{' '}
                  <code className="bg-yellow-100 px-1 rounded">mcp-scan</code> locally
                  and upload the results after registration.
                </p>
              </div>
            )}

            {formData.sourceType === 'ContainerImage' && (
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Container Image
                </label>
                <input
                  type="text"
                  value={formData.mcpConfig.image || ''}
                  onChange={(e) =>
                    setFormData({
                      ...formData,
                      mcpConfig: { ...formData.mcpConfig, image: e.target.value },
                    })
                  }
                  className="w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 font-mono text-sm"
                  placeholder="ghcr.io/org/mcp-server:v1.0"
                />
              </div>
            )}

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Declared Tools
              </label>
              <input
                type="text"
                value={toolsInput}
                onChange={(e) => setToolsInput(e.target.value)}
                className="w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500"
                placeholder="search, read_file, write_file (comma-separated)"
              />
              <p className="text-xs text-gray-500 mt-1">
                List of tools this server provides (will be verified during scan)
              </p>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Description
              </label>
              <textarea
                value={formData.description || ''}
                onChange={(e) =>
                  setFormData({ ...formData, description: e.target.value })
                }
                rows={2}
                className="w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500"
                placeholder="Optional description of what this server does"
              />
            </div>

            <div className="flex justify-end gap-3 pt-4 border-t">
              <button
                type="button"
                onClick={onClose}
                className="px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={isSubmitting}
                className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50"
              >
                {isSubmitting ? 'Registering...' : 'Register Server'}
              </button>
            </div>
          </form>
        ) : (
          <div className="p-4 space-y-4">
            <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
              <h3 className="font-medium text-blue-900 mb-2 flex items-center gap-2">
                <Terminal className="w-4 h-4" />
                Run MCP-Scan Locally
              </h3>
              <p className="text-sm text-blue-800 mb-3">
                Run the following command in your terminal to scan your local server:
              </p>
              <pre className="bg-blue-900 text-blue-100 p-3 rounded-lg text-sm overflow-x-auto">
{`# Install mcp-scan if not already installed
pip install mcp-scan

# Run the scan and save output
mcp-scan scan --config ~/.config/mcp/config.json --output-format json > scan-results.json

# Copy the contents of scan-results.json below`}
              </pre>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Paste Scan Results (JSON)
              </label>
              <textarea
                value={scanOutput}
                onChange={(e) => setScanOutput(e.target.value)}
                rows={10}
                className="w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 font-mono text-xs"
                placeholder='{"riskScore": 25, "issues": [], "tools": [...]}'
              />
            </div>

            <div className="flex justify-end gap-3 pt-4 border-t">
              <button
                type="button"
                onClick={() => setStep('form')}
                className="px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg"
              >
                Back
              </button>
              <button
                onClick={handleScanUpload}
                disabled={isSubmitting || !scanOutput}
                className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50"
              >
                <Upload className="w-4 h-4" />
                {isSubmitting ? 'Uploading...' : 'Upload Scan Results'}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
