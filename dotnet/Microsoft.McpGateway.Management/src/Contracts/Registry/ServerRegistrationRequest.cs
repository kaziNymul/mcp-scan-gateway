// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.McpGateway.Management.Contracts.Registry
{
    /// <summary>
    /// Request model for registering a new MCP server in the registry.
    /// </summary>
    public class ServerRegistrationRequest
    {
        /// <summary>
        /// Canonical identifier for the server. Must match the server ID used in MCP client configurations.
        /// Example: "weather-server", "filesystem-tools", "my-company/internal-tools"
        /// </summary>
        [JsonPropertyName("canonicalId")]
        public required string CanonicalId { get; set; }

        /// <summary>
        /// Human-readable display name for the server.
        /// </summary>
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        /// <summary>
        /// Description of what the server does and its capabilities.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Team or organization that owns this server registration.
        /// Used for access control and audit purposes.
        /// </summary>
        [JsonPropertyName("ownerTeam")]
        public required string OwnerTeam { get; set; }

        /// <summary>
        /// Type of source for this server.
        /// </summary>
        [JsonPropertyName("sourceType")]
        public required SourceType SourceType { get; set; }

        /// <summary>
        /// URL to the source repository, package, or artifact.
        /// For LocalDeclared, this may be empty or point to documentation.
        /// </summary>
        [JsonPropertyName("sourceUrl")]
        public string? SourceUrl { get; set; }

        /// <summary>
        /// Version of the server being registered.
        /// </summary>
        [JsonPropertyName("version")]
        public required string Version { get; set; }

        /// <summary>
        /// Optional list of declared tools that this server provides.
        /// Used for scanning and policy enforcement.
        /// </summary>
        [JsonPropertyName("declaredTools")]
        public List<DeclaredTool>? DeclaredTools { get; set; }

        /// <summary>
        /// Optional MCP configuration for scanning.
        /// Can be used to test server connectivity during scanning.
        /// </summary>
        [JsonPropertyName("mcpConfig")]
        public McpServerConfig? McpConfig { get; set; }

        /// <summary>
        /// Optional test endpoint URL for dynamic scanning.
        /// If provided, the scanner can connect to verify tool signatures.
        /// </summary>
        [JsonPropertyName("testEndpoint")]
        public string? TestEndpoint { get; set; }

        /// <summary>
        /// Optional tags for categorization and filtering.
        /// </summary>
        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }
    }

    /// <summary>
    /// Represents a tool declared by the server owner.
    /// </summary>
    public class DeclaredTool
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        /// <summary>
        /// Input schema as JSON Schema.
        /// </summary>
        [JsonPropertyName("inputSchema")]
        public object? InputSchema { get; set; }
    }

    /// <summary>
    /// MCP server configuration for scanning.
    /// </summary>
    public class McpServerConfig
    {
        /// <summary>
        /// Transport type: "stdio" or "http"
        /// </summary>
        [JsonPropertyName("transport")]
        public required string Transport { get; set; }

        /// <summary>
        /// For stdio: the command to run
        /// </summary>
        [JsonPropertyName("command")]
        public string? Command { get; set; }

        /// <summary>
        /// For stdio: command arguments
        /// </summary>
        [JsonPropertyName("args")]
        public List<string>? Args { get; set; }

        /// <summary>
        /// For http: the server URL
        /// </summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        /// <summary>
        /// Environment variables (values should be references, not secrets)
        /// </summary>
        [JsonPropertyName("env")]
        public Dictionary<string, string>? Env { get; set; }
    }
}
