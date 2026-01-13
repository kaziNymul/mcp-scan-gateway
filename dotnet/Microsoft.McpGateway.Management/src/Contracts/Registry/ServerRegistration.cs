// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.McpGateway.Management.Contracts.Registry
{
    /// <summary>
    /// Represents a registered MCP server in the registry.
    /// </summary>
    public class ServerRegistration
    {
        /// <summary>
        /// Unique database identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public required Guid Id { get; set; }

        /// <summary>
        /// Canonical identifier for the server. Must match the server ID used in MCP client configurations.
        /// </summary>
        [JsonPropertyName("canonicalId")]
        public required string CanonicalId { get; set; }

        /// <summary>
        /// Human-readable display name.
        /// </summary>
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        /// <summary>
        /// Description of the server.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Team that owns this registration.
        /// </summary>
        [JsonPropertyName("ownerTeam")]
        public required string OwnerTeam { get; set; }

        /// <summary>
        /// Type of source.
        /// </summary>
        [JsonPropertyName("sourceType")]
        public required SourceType SourceType { get; set; }

        /// <summary>
        /// URL to the source.
        /// </summary>
        [JsonPropertyName("sourceUrl")]
        public string? SourceUrl { get; set; }

        /// <summary>
        /// Version of the server.
        /// </summary>
        [JsonPropertyName("version")]
        public required string Version { get; set; }

        /// <summary>
        /// Current status in the governance lifecycle.
        /// </summary>
        [JsonPropertyName("status")]
        public required ServerStatus Status { get; set; }

        /// <summary>
        /// Declared tools.
        /// </summary>
        [JsonPropertyName("declaredTools")]
        public List<DeclaredTool>? DeclaredTools { get; set; }

        /// <summary>
        /// MCP configuration for scanning.
        /// </summary>
        [JsonPropertyName("mcpConfig")]
        public McpServerConfig? McpConfig { get; set; }

        /// <summary>
        /// Test endpoint URL.
        /// </summary>
        [JsonPropertyName("testEndpoint")]
        public string? TestEndpoint { get; set; }

        /// <summary>
        /// Tags for categorization.
        /// </summary>
        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        /// <summary>
        /// User ID who created this registration.
        /// </summary>
        [JsonPropertyName("createdBy")]
        public required string CreatedBy { get; set; }

        /// <summary>
        /// When the registration was created.
        /// </summary>
        [JsonPropertyName("createdAt")]
        public required DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// When the registration was last updated.
        /// </summary>
        [JsonPropertyName("updatedAt")]
        public required DateTimeOffset UpdatedAt { get; set; }

        /// <summary>
        /// Latest scan ID if any scan has been performed.
        /// </summary>
        [JsonPropertyName("latestScanId")]
        public Guid? LatestScanId { get; set; }

        /// <summary>
        /// Latest scan risk score (0.0 = safe, 1.0 = high risk).
        /// </summary>
        [JsonPropertyName("latestRiskScore")]
        public double? LatestRiskScore { get; set; }

        /// <summary>
        /// Creates a new registration from a request.
        /// </summary>
        public static ServerRegistration Create(ServerRegistrationRequest request, string createdBy)
        {
            var now = DateTimeOffset.UtcNow;
            return new ServerRegistration
            {
                Id = Guid.NewGuid(),
                CanonicalId = request.CanonicalId,
                Name = request.Name,
                Description = request.Description,
                OwnerTeam = request.OwnerTeam,
                SourceType = request.SourceType,
                SourceUrl = request.SourceUrl,
                Version = request.Version,
                Status = ServerStatus.Draft,
                DeclaredTools = request.DeclaredTools,
                McpConfig = request.McpConfig,
                TestEndpoint = request.TestEndpoint,
                Tags = request.Tags,
                CreatedBy = createdBy,
                CreatedAt = now,
                UpdatedAt = now
            };
        }
    }
}
