// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.McpGateway.Management.Contracts.Registry
{
    /// <summary>
    /// Status of a security scan.
    /// </summary>
    public enum ScanStatus
    {
        /// <summary>
        /// Scan has been queued but not yet started.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// Scan is currently running.
        /// </summary>
        Running = 1,

        /// <summary>
        /// Scan completed successfully with results.
        /// </summary>
        Completed = 2,

        /// <summary>
        /// Scan failed due to an error.
        /// </summary>
        Failed = 3,

        /// <summary>
        /// Scan was cancelled.
        /// </summary>
        Cancelled = 4,

        /// <summary>
        /// Scan timed out.
        /// </summary>
        TimedOut = 5
    }

    /// <summary>
    /// Represents a security scan result for an MCP server.
    /// </summary>
    public class ScanResult
    {
        /// <summary>
        /// Unique identifier for this scan.
        /// </summary>
        [JsonPropertyName("id")]
        public required Guid Id { get; set; }

        /// <summary>
        /// ID of the server that was scanned.
        /// </summary>
        [JsonPropertyName("serverId")]
        public required Guid ServerId { get; set; }

        /// <summary>
        /// Canonical ID of the server.
        /// </summary>
        [JsonPropertyName("serverCanonicalId")]
        public required string ServerCanonicalId { get; set; }

        /// <summary>
        /// Version of the scanner used.
        /// </summary>
        [JsonPropertyName("scannerVersion")]
        public required string ScannerVersion { get; set; }

        /// <summary>
        /// Current status of the scan.
        /// </summary>
        [JsonPropertyName("status")]
        public required ScanStatus Status { get; set; }

        /// <summary>
        /// Risk score from 0.0 (safe) to 1.0 (high risk).
        /// </summary>
        [JsonPropertyName("riskScore")]
        public double? RiskScore { get; set; }

        /// <summary>
        /// Human-readable summary of the scan findings.
        /// </summary>
        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        /// <summary>
        /// Full scan report as JSON.
        /// </summary>
        [JsonPropertyName("reportJson")]
        public string? ReportJson { get; set; }

        /// <summary>
        /// List of issues found during the scan.
        /// </summary>
        [JsonPropertyName("issues")]
        public List<ScanIssue>? Issues { get; set; }

        /// <summary>
        /// List of tools discovered during the scan.
        /// </summary>
        [JsonPropertyName("discoveredTools")]
        public List<DiscoveredTool>? DiscoveredTools { get; set; }

        /// <summary>
        /// Kubernetes Job name that ran this scan.
        /// </summary>
        [JsonPropertyName("jobName")]
        public string? JobName { get; set; }

        /// <summary>
        /// Error message if the scan failed.
        /// </summary>
        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// When the scan was started.
        /// </summary>
        [JsonPropertyName("startedAt")]
        public required DateTimeOffset StartedAt { get; set; }

        /// <summary>
        /// When the scan finished.
        /// </summary>
        [JsonPropertyName("finishedAt")]
        public DateTimeOffset? FinishedAt { get; set; }

        /// <summary>
        /// User who triggered the scan.
        /// </summary>
        [JsonPropertyName("triggeredBy")]
        public required string TriggeredBy { get; set; }

        /// <summary>
        /// Creates a new pending scan.
        /// </summary>
        public static ScanResult CreatePending(Guid serverId, string serverCanonicalId, string scannerVersion, string triggeredBy)
        {
            return new ScanResult
            {
                Id = Guid.NewGuid(),
                ServerId = serverId,
                ServerCanonicalId = serverCanonicalId,
                ScannerVersion = scannerVersion,
                Status = ScanStatus.Pending,
                StartedAt = DateTimeOffset.UtcNow,
                TriggeredBy = triggeredBy
            };
        }
    }

    /// <summary>
    /// Represents an issue found during scanning.
    /// </summary>
    public class ScanIssue
    {
        /// <summary>
        /// Issue code (e.g., "W001", "E001").
        /// </summary>
        [JsonPropertyName("code")]
        public required string Code { get; set; }

        /// <summary>
        /// Severity: info, warning, error, critical.
        /// </summary>
        [JsonPropertyName("severity")]
        public required string Severity { get; set; }

        /// <summary>
        /// Issue message.
        /// </summary>
        [JsonPropertyName("message")]
        public required string Message { get; set; }

        /// <summary>
        /// Tool or entity affected, if applicable.
        /// </summary>
        [JsonPropertyName("affectedEntity")]
        public string? AffectedEntity { get; set; }

        /// <summary>
        /// Recommended remediation.
        /// </summary>
        [JsonPropertyName("remediation")]
        public string? Remediation { get; set; }
    }

    /// <summary>
    /// Represents a tool discovered during scanning.
    /// </summary>
    public class DiscoveredTool
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("descriptionHash")]
        public string? DescriptionHash { get; set; }

        /// <summary>
        /// Tool classification labels from MCP-Scan.
        /// </summary>
        [JsonPropertyName("labels")]
        public ToolLabels? Labels { get; set; }
    }

    /// <summary>
    /// Tool risk classification labels from MCP-Scan.
    /// </summary>
    public class ToolLabels
    {
        /// <summary>
        /// Whether the tool can write to public endpoints.
        /// </summary>
        [JsonPropertyName("isPublicSink")]
        public double IsPublicSink { get; set; }

        /// <summary>
        /// Whether the tool performs destructive operations.
        /// </summary>
        [JsonPropertyName("destructive")]
        public double Destructive { get; set; }

        /// <summary>
        /// Whether the tool processes untrusted content.
        /// </summary>
        [JsonPropertyName("untrustedContent")]
        public double UntrustedContent { get; set; }

        /// <summary>
        /// Whether the tool accesses private data.
        /// </summary>
        [JsonPropertyName("privateData")]
        public double PrivateData { get; set; }
    }

    /// <summary>
    /// Request to upload local scan results.
    /// </summary>
    /// <remarks>
    /// Used when a server is running locally and cannot be scanned by the K8s job.
    /// Users run mcp-scan locally and upload the JSON output.
    /// </remarks>
    public class LocalScanUploadRequest
    {
        /// <summary>
        /// The JSON output from mcp-scan CLI.
        /// </summary>
        [JsonPropertyName("scanOutput")]
        public required string ScanOutput { get; set; }

        /// <summary>
        /// Version of mcp-scan used.
        /// </summary>
        [JsonPropertyName("scanVersion")]
        public required string ScanVersion { get; set; }

        /// <summary>
        /// When the scan was performed (ISO 8601).
        /// </summary>
        [JsonPropertyName("scannedAt")]
        public required string ScannedAt { get; set; }
    }
}
