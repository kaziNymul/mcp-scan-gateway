// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.McpGateway.Management.Contracts.Registry
{
    /// <summary>
    /// Represents the type of source for an MCP server.
    /// </summary>
    public enum SourceType
    {
        /// <summary>
        /// Server sourced from an external public repository (e.g., GitHub, npm).
        /// </summary>
        ExternalRepo = 0,

        /// <summary>
        /// Server sourced from an internal private repository.
        /// </summary>
        InternalRepo = 1,

        /// <summary>
        /// Server declared locally by the user without a repository reference.
        /// Requires manifest/tool declaration for scanning.
        /// </summary>
        LocalDeclared = 2,

        /// <summary>
        /// Server deployed as a container image.
        /// </summary>
        ContainerImage = 3,

        /// <summary>
        /// Server available as a packaged artifact (e.g., npm package, PyPI package).
        /// </summary>
        PackageArtifact = 4
    }
}
