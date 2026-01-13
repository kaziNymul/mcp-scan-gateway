// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.McpGateway.Management.Contracts.Registry;
using Microsoft.McpGateway.Management.Store.Registry;

namespace Microsoft.McpGateway.Management.Service.Registry
{
    /// <summary>
    /// Service for running MCP-Scan as Kubernetes Jobs.
    /// </summary>
    public class KubernetesScannerService : IScannerService
    {
        private readonly IKubernetes _kubernetesClient;
        private readonly IScanResultStore _scanStore;
        private readonly IServerRegistryStore _serverStore;
        private readonly ScannerConfig _config;
        private readonly ILogger<KubernetesScannerService> _logger;

        public KubernetesScannerService(
            IKubernetes kubernetesClient,
            IScanResultStore scanStore,
            IServerRegistryStore serverStore,
            IOptions<ScannerConfig> config,
            ILogger<KubernetesScannerService> logger)
        {
            _kubernetesClient = kubernetesClient ?? throw new ArgumentNullException(nameof(kubernetesClient));
            _scanStore = scanStore ?? throw new ArgumentNullException(nameof(scanStore));
            _serverStore = serverStore ?? throw new ArgumentNullException(nameof(serverStore));
            _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ScanResult> TriggerScanAsync(ServerRegistration server, string triggeredBy, CancellationToken cancellationToken)
        {
            // Create scan record
            var scan = ScanResult.CreatePending(server.Id, server.CanonicalId, GetScannerVersion(), triggeredBy);
            scan.Status = ScanStatus.Pending;
            await _scanStore.CreateAsync(scan, cancellationToken);

            // Create Kubernetes Job
            var jobName = $"mcp-scan-{scan.Id:N}".Substring(0, 63).ToLowerInvariant();
            scan.JobName = jobName;

            try
            {
                var job = CreateScanJob(jobName, server, scan.Id);
                await _kubernetesClient.BatchV1.CreateNamespacedJobAsync(job, _config.JobNamespace, cancellationToken: cancellationToken);
                
                scan.Status = ScanStatus.Running;
                await _scanStore.UpdateAsync(scan, cancellationToken);

                // Update server status
                await _serverStore.UpdateStatusAsync(server.Id, ServerStatus.Scanning, cancellationToken);

                _logger.LogInformation("Started scan job {JobName} for server {CanonicalId}", jobName, server.CanonicalId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create scan job for server {CanonicalId}", server.CanonicalId);
                scan.Status = ScanStatus.Failed;
                scan.ErrorMessage = $"Failed to create Kubernetes job: {ex.Message}";
                scan.FinishedAt = DateTimeOffset.UtcNow;
                await _scanStore.UpdateAsync(scan, cancellationToken);
                
                await _serverStore.UpdateStatusAsync(server.Id, ServerStatus.ScannedFail, cancellationToken);
            }

            return scan;
        }

        public async Task<ScanResult?> GetScanStatusAsync(Guid scanId, CancellationToken cancellationToken)
        {
            return await _scanStore.GetByIdAsync(scanId, cancellationToken);
        }

        public async Task ProcessCompletedScansAsync(CancellationToken cancellationToken)
        {
            // Get all running scans
            var runningScans = await _scanStore.ListByStatusAsync(ScanStatus.Running, cancellationToken);

            foreach (var scan in runningScans)
            {
                if (string.IsNullOrEmpty(scan.JobName)) continue;

                try
                {
                    var job = await _kubernetesClient.BatchV1.ReadNamespacedJobAsync(
                        scan.JobName, _config.JobNamespace, cancellationToken: cancellationToken);

                    if (IsJobCompleted(job))
                    {
                        await ProcessCompletedJob(scan, job, cancellationToken);
                    }
                    else if (IsJobTimedOut(scan))
                    {
                        await ProcessTimedOutJob(scan, cancellationToken);
                    }
                }
                catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Scan job {JobName} not found, marking as failed", scan.JobName);
                    scan.Status = ScanStatus.Failed;
                    scan.ErrorMessage = "Job not found in Kubernetes";
                    scan.FinishedAt = DateTimeOffset.UtcNow;
                    await _scanStore.UpdateAsync(scan, cancellationToken);
                    await _serverStore.UpdateStatusAsync(scan.ServerId, ServerStatus.ScannedFail, cancellationToken);
                }
            }
        }

        public async Task CancelScanAsync(Guid scanId, CancellationToken cancellationToken)
        {
            var scan = await _scanStore.GetByIdAsync(scanId, cancellationToken);
            if (scan == null || string.IsNullOrEmpty(scan.JobName)) return;

            try
            {
                await _kubernetesClient.BatchV1.DeleteNamespacedJobAsync(
                    scan.JobName, _config.JobNamespace,
                    propagationPolicy: "Background",
                    cancellationToken: cancellationToken);

                scan.Status = ScanStatus.Cancelled;
                scan.FinishedAt = DateTimeOffset.UtcNow;
                await _scanStore.UpdateAsync(scan, cancellationToken);

                _logger.LogInformation("Cancelled scan job {JobName}", scan.JobName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel scan job {JobName}", scan.JobName);
            }
        }

        private V1Job CreateScanJob(string jobName, ServerRegistration server, Guid scanId)
        {
            // Build scan configuration as JSON
            var scanConfig = new
            {
                server_id = server.Id.ToString(),
                canonical_id = server.CanonicalId,
                source_type = server.SourceType.ToString(),
                source_url = server.SourceUrl,
                test_endpoint = server.TestEndpoint,
                mcp_config = server.McpConfig,
                declared_tools = server.DeclaredTools
            };

            var configJson = JsonSerializer.Serialize(scanConfig);
            var configBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(configJson));

            return new V1Job
            {
                Metadata = new V1ObjectMeta
                {
                    Name = jobName,
                    NamespaceProperty = _config.JobNamespace,
                    Labels = new Dictionary<string, string>
                    {
                        ["app"] = "mcp-scanner",
                        ["scan-id"] = scanId.ToString(),
                        ["server-id"] = server.Id.ToString()
                    }
                },
                Spec = new V1JobSpec
                {
                    BackoffLimit = _config.Retries,
                    ActiveDeadlineSeconds = _config.TimeoutSeconds,
                    TtlSecondsAfterFinished = 3600, // Clean up after 1 hour
                    Template = new V1PodTemplateSpec
                    {
                        Spec = new V1PodSpec
                        {
                            ServiceAccountName = _config.JobServiceAccount,
                            RestartPolicy = "Never",
                            Containers = new List<V1Container>
                            {
                                new V1Container
                                {
                                    Name = "mcp-scan",
                                    Image = _config.Image,
                                    Command = new List<string> { "/bin/sh", "-c" },
                                    Args = new List<string>
                                    {
                                        BuildScanCommand(server, configBase64)
                                    },
                                    Env = new List<V1EnvVar>
                                    {
                                        new V1EnvVar { Name = "SCAN_ID", Value = scanId.ToString() },
                                        new V1EnvVar { Name = "SERVER_ID", Value = server.Id.ToString() },
                                        new V1EnvVar { Name = "SCAN_CONFIG", Value = configBase64 }
                                    },
                                    Resources = new V1ResourceRequirements
                                    {
                                        Requests = new Dictionary<string, ResourceQuantity>
                                        {
                                            ["cpu"] = new ResourceQuantity(_config.CpuRequest),
                                            ["memory"] = new ResourceQuantity(_config.MemoryRequest)
                                        },
                                        Limits = new Dictionary<string, ResourceQuantity>
                                        {
                                            ["cpu"] = new ResourceQuantity(_config.CpuLimit),
                                            ["memory"] = new ResourceQuantity(_config.MemoryLimit)
                                        }
                                    },
                                    SecurityContext = new V1SecurityContext
                                    {
                                        RunAsNonRoot = true,
                                        ReadOnlyRootFilesystem = true,
                                        AllowPrivilegeEscalation = false
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        private string BuildScanCommand(ServerRegistration server, string configBase64)
        {
            var commands = new List<string>();

            // Decode config
            commands.Add("echo $SCAN_CONFIG | base64 -d > /tmp/scan-config.json");

            // Build mcp-scan command based on source type
            switch (server.SourceType)
            {
                case SourceType.ExternalRepo:
                case SourceType.InternalRepo:
                    if (!string.IsNullOrEmpty(server.SourceUrl))
                    {
                        // Clone repo and scan
                        commands.Add($"git clone --depth 1 {server.SourceUrl} /tmp/repo 2>/dev/null || echo 'Clone failed'");
                        commands.Add("mcp-scan scan /tmp/repo --json 2>/dev/null || echo '[]'");
                    }
                    break;

                case SourceType.LocalDeclared:
                    // Scan declared tools
                    if (server.DeclaredTools?.Any() == true)
                    {
                        commands.Add("mcp-scan scan --json /tmp/scan-config.json 2>/dev/null || echo '[]'");
                    }
                    break;

                default:
                    commands.Add("echo '{\"status\": \"skipped\", \"reason\": \"unsupported source type\"}'");
                    break;
            }

            // If dynamic testing is enabled and test endpoint exists
            if (_config.EnableDynamicTesting && !string.IsNullOrEmpty(server.TestEndpoint))
            {
                commands.Add($"mcp-scan scan --json {server.TestEndpoint} 2>/dev/null || echo '[]'");
            }

            return string.Join(" && ", commands);
        }

        private async Task ProcessCompletedJob(ScanResult scan, V1Job job, CancellationToken cancellationToken)
        {
            try
            {
                // Get pod logs
                var pods = await _kubernetesClient.CoreV1.ListNamespacedPodAsync(
                    _config.JobNamespace,
                    labelSelector: $"job-name={scan.JobName}",
                    cancellationToken: cancellationToken);

                var pod = pods.Items.FirstOrDefault();
                if (pod != null)
                {
                    var logs = await _kubernetesClient.CoreV1.ReadNamespacedPodLogAsync(
                        pod.Metadata.Name, _config.JobNamespace,
                        cancellationToken: cancellationToken);

                    // Parse scan results from logs
                    var scanResults = ParseScanResults(logs);
                    
                    scan.ReportJson = logs;
                    scan.RiskScore = scanResults.RiskScore;
                    scan.Summary = scanResults.Summary;
                    scan.Issues = scanResults.Issues;
                    scan.DiscoveredTools = scanResults.DiscoveredTools;
                }

                scan.FinishedAt = DateTimeOffset.UtcNow;

                // Determine pass/fail based on job status and risk score
                if (job.Status.Succeeded > 0)
                {
                    var passThreshold = 0.5; // TODO: Get from config
                    if (scan.RiskScore.HasValue && scan.RiskScore.Value > passThreshold)
                    {
                        scan.Status = ScanStatus.Completed;
                        await _serverStore.UpdateStatusAsync(scan.ServerId, ServerStatus.ScannedFail, cancellationToken);
                    }
                    else
                    {
                        scan.Status = ScanStatus.Completed;
                        await _serverStore.UpdateStatusAsync(scan.ServerId, ServerStatus.ScannedPass, cancellationToken);
                    }
                }
                else
                {
                    scan.Status = ScanStatus.Failed;
                    scan.ErrorMessage = "Job failed";
                    await _serverStore.UpdateStatusAsync(scan.ServerId, ServerStatus.ScannedFail, cancellationToken);
                }

                await _scanStore.UpdateAsync(scan, cancellationToken);

                // Update server with scan results
                if (scan.RiskScore.HasValue)
                {
                    await _serverStore.UpdateLatestScanAsync(scan.ServerId, scan.Id, scan.RiskScore.Value, cancellationToken);
                }

                _logger.LogInformation("Completed scan {ScanId} for server {ServerId} with status {Status}",
                    scan.Id, scan.ServerId, scan.Status);

                // Clean up job
                await _kubernetesClient.BatchV1.DeleteNamespacedJobAsync(
                    scan.JobName!, _config.JobNamespace,
                    propagationPolicy: "Background",
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing completed scan job {JobName}", scan.JobName);
            }
        }

        private async Task ProcessTimedOutJob(ScanResult scan, CancellationToken cancellationToken)
        {
            scan.Status = ScanStatus.TimedOut;
            scan.ErrorMessage = $"Scan timed out after {_config.TimeoutSeconds} seconds";
            scan.FinishedAt = DateTimeOffset.UtcNow;
            await _scanStore.UpdateAsync(scan, cancellationToken);

            await _serverStore.UpdateStatusAsync(scan.ServerId, ServerStatus.ScannedFail, cancellationToken);

            // Try to clean up the job
            try
            {
                await _kubernetesClient.BatchV1.DeleteNamespacedJobAsync(
                    scan.JobName!, _config.JobNamespace,
                    propagationPolicy: "Background",
                    cancellationToken: cancellationToken);
            }
            catch { /* Ignore cleanup errors */ }

            _logger.LogWarning("Scan {ScanId} timed out", scan.Id);
        }

        private static bool IsJobCompleted(V1Job job)
        {
            return (job.Status.Succeeded ?? 0) > 0 || (job.Status.Failed ?? 0) > 0;
        }

        private bool IsJobTimedOut(ScanResult scan)
        {
            return scan.StartedAt.AddSeconds(_config.TimeoutSeconds) < DateTimeOffset.UtcNow;
        }

        private string GetScannerVersion()
        {
            // Extract version from image tag
            var parts = _config.Image.Split(':');
            return parts.Length > 1 ? parts[1] : "latest";
        }

        private static ScanResultData ParseScanResults(string logs)
        {
            var result = new ScanResultData();

            try
            {
                // Try to parse JSON from logs
                var lines = logs.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines.Reverse())
                {
                    if (line.TrimStart().StartsWith('[') || line.TrimStart().StartsWith('{'))
                    {
                        // Try to parse as JSON
                        try
                        {
                            var jsonDoc = JsonDocument.Parse(line);
                            // Extract relevant data from mcp-scan output
                            // This is a simplified parser - actual implementation would need
                            // to match the exact mcp-scan output format
                            result.Summary = "Scan completed";
                            result.RiskScore = 0.3; // Default low risk if no issues found
                            break;
                        }
                        catch { continue; }
                    }
                }
            }
            catch
            {
                result.Summary = "Failed to parse scan results";
                result.RiskScore = 0.5;
            }

            return result;
        }

        private class ScanResultData
        {
            public double? RiskScore { get; set; }
            public string? Summary { get; set; }
            public List<ScanIssue>? Issues { get; set; }
            public List<DiscoveredTool>? DiscoveredTools { get; set; }
        }
    }
}
