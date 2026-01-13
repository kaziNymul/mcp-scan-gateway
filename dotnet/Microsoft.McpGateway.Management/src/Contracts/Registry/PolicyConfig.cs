// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.McpGateway.Management.Contracts.Registry
{
    /// <summary>
    /// Policy configuration for MCP governance.
    /// </summary>
    public class PolicyConfig
    {
        /// <summary>
        /// Global list of tool names that are denied for all servers.
        /// </summary>
        [JsonPropertyName("globalToolDenylist")]
        public List<string> GlobalToolDenylist { get; set; } = new();

        /// <summary>
        /// Global list of tool categories that are denied.
        /// </summary>
        [JsonPropertyName("deniedToolCategories")]
        public List<string> DeniedToolCategories { get; set; } = new();

        /// <summary>
        /// Per-team allowlists. Key is team name, value is list of allowed server canonical IDs.
        /// </summary>
        [JsonPropertyName("teamAllowlists")]
        public Dictionary<string, List<string>> TeamAllowlists { get; set; } = new();

        /// <summary>
        /// Per-team denylists. Key is team name, value is list of denied server canonical IDs.
        /// </summary>
        [JsonPropertyName("teamDenylists")]
        public Dictionary<string, List<string>> TeamDenylists { get; set; } = new();

        /// <summary>
        /// Rate limits per user (requests per minute).
        /// </summary>
        [JsonPropertyName("rateLimitPerUser")]
        public int RateLimitPerUser { get; set; } = 60;

        /// <summary>
        /// Rate limits per team (requests per minute).
        /// </summary>
        [JsonPropertyName("rateLimitPerTeam")]
        public int RateLimitPerTeam { get; set; } = 600;

        /// <summary>
        /// Default timeout for tool calls in milliseconds.
        /// </summary>
        [JsonPropertyName("defaultTimeoutMs")]
        public int DefaultTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// Maximum request payload size in bytes.
        /// </summary>
        [JsonPropertyName("maxRequestPayloadBytes")]
        public long MaxRequestPayloadBytes { get; set; } = 1048576; // 1MB

        /// <summary>
        /// Maximum response payload size in bytes.
        /// </summary>
        [JsonPropertyName("maxResponsePayloadBytes")]
        public long MaxResponsePayloadBytes { get; set; } = 10485760; // 10MB

        /// <summary>
        /// Minimum risk score that triggers additional controls (0.0 to 1.0).
        /// </summary>
        [JsonPropertyName("riskThreshold")]
        public double RiskThreshold { get; set; } = 0.7;

        /// <summary>
        /// Minimum passing score for scans (0.0 to 1.0, where lower is better).
        /// Servers with risk scores above this threshold fail scanning.
        /// </summary>
        [JsonPropertyName("scanPassThreshold")]
        public double ScanPassThreshold { get; set; } = 0.5;

        /// <summary>
        /// Whether to require admin role for high-risk server access.
        /// </summary>
        [JsonPropertyName("requireAdminForHighRisk")]
        public bool RequireAdminForHighRisk { get; set; } = true;

        /// <summary>
        /// Whether to enforce registry-only mode (only approved servers allowed).
        /// </summary>
        [JsonPropertyName("enforceRegistryOnly")]
        public bool EnforceRegistryOnly { get; set; } = true;

        /// <summary>
        /// Whether to allow bypass for specific users/service accounts (for emergency use).
        /// </summary>
        [JsonPropertyName("bypassAllowedPrincipals")]
        public List<string> BypassAllowedPrincipals { get; set; } = new();
    }

    /// <summary>
    /// Scanner configuration.
    /// </summary>
    public class ScannerConfig
    {
        /// <summary>
        /// Docker image for the MCP-Scan scanner.
        /// </summary>
        [JsonPropertyName("image")]
        public string Image { get; set; } = "ghcr.io/invariantlabs-ai/mcp-scan:latest";

        /// <summary>
        /// Timeout for scan jobs in seconds.
        /// </summary>
        [JsonPropertyName("timeoutSeconds")]
        public int TimeoutSeconds { get; set; } = 300;

        /// <summary>
        /// Number of retries for failed scans.
        /// </summary>
        [JsonPropertyName("retries")]
        public int Retries { get; set; } = 1;

        /// <summary>
        /// Namespace for scanner jobs.
        /// </summary>
        [JsonPropertyName("jobNamespace")]
        public string JobNamespace { get; set; } = "mcp-gov";

        /// <summary>
        /// Service account for scanner jobs.
        /// </summary>
        [JsonPropertyName("jobServiceAccount")]
        public string JobServiceAccount { get; set; } = "mcp-scanner-sa";

        /// <summary>
        /// CPU request for scanner jobs.
        /// </summary>
        [JsonPropertyName("cpuRequest")]
        public string CpuRequest { get; set; } = "100m";

        /// <summary>
        /// Memory request for scanner jobs.
        /// </summary>
        [JsonPropertyName("memoryRequest")]
        public string MemoryRequest { get; set; } = "256Mi";

        /// <summary>
        /// CPU limit for scanner jobs.
        /// </summary>
        [JsonPropertyName("cpuLimit")]
        public string CpuLimit { get; set; } = "500m";

        /// <summary>
        /// Memory limit for scanner jobs.
        /// </summary>
        [JsonPropertyName("memoryLimit")]
        public string MemoryLimit { get; set; } = "512Mi";

        /// <summary>
        /// Whether to enable dynamic testing (connecting to test endpoints).
        /// </summary>
        [JsonPropertyName("enableDynamicTesting")]
        public bool EnableDynamicTesting { get; set; } = false;

        /// <summary>
        /// Analysis API URL for remote risk analysis.
        /// </summary>
        [JsonPropertyName("analysisApiUrl")]
        public string? AnalysisApiUrl { get; set; }
    }

    /// <summary>
    /// Overall jurisdiction configuration.
    /// </summary>
    public class JurisdictionConfig
    {
        /// <summary>
        /// Whether jurisdiction enforcement is enabled.
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Enforcement mode: Audit (log only) or Enforce (block).
        /// </summary>
        [JsonPropertyName("enforcementMode")]
        public EnforcementMode EnforcementMode { get; set; } = EnforcementMode.Enforce;

        /// <summary>
        /// Policy configuration.
        /// </summary>
        [JsonPropertyName("policy")]
        public PolicyConfig Policy { get; set; } = new();

        /// <summary>
        /// Scanner configuration.
        /// </summary>
        [JsonPropertyName("scanner")]
        public ScannerConfig Scanner { get; set; } = new();

        /// <summary>
        /// PostgreSQL connection string.
        /// </summary>
        [JsonPropertyName("postgresConnection")]
        public string? PostgresConnection { get; set; }
    }

    /// <summary>
    /// Enforcement mode for jurisdiction.
    /// </summary>
    public enum EnforcementMode
    {
        /// <summary>
        /// Only audit and log, do not block.
        /// </summary>
        Audit = 0,

        /// <summary>
        /// Enforce policies and block non-compliant requests.
        /// </summary>
        Enforce = 1
    }
}
