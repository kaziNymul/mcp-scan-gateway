// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.McpGateway.Management.Contracts.Registry
{
    /// <summary>
    /// Represents the status of a server in the registry.
    /// Follows the governance lifecycle: submission → scanning → approval → active use.
    /// </summary>
    public enum ServerStatus
    {
        /// <summary>
        /// Initial state when server is first created but not yet submitted for scanning.
        /// </summary>
        Draft = 0,

        /// <summary>
        /// Server has been submitted and is queued for security scanning.
        /// </summary>
        PendingScan = 1,

        /// <summary>
        /// Security scan is currently in progress.
        /// </summary>
        Scanning = 2,

        /// <summary>
        /// Security scan completed successfully with acceptable risk score.
        /// Server can now be submitted for admin approval.
        /// </summary>
        ScannedPass = 3,

        /// <summary>
        /// Security scan completed but found unacceptable risks.
        /// Server cannot proceed to approval without remediation.
        /// </summary>
        ScannedFail = 4,

        /// <summary>
        /// Server has passed scanning and is awaiting admin approval.
        /// </summary>
        PendingApproval = 5,

        /// <summary>
        /// Server has been approved by admin and is authorized for use.
        /// Tool calls through the gateway will be allowed.
        /// </summary>
        Approved = 6,

        /// <summary>
        /// Server approval was denied by admin.
        /// Tool calls through the gateway will be blocked.
        /// </summary>
        Denied = 7,

        /// <summary>
        /// Server was previously approved but has been deprecated.
        /// New connections are discouraged but existing ones may work.
        /// </summary>
        Deprecated = 8,

        /// <summary>
        /// Server has been suspended due to policy violation or security concern.
        /// All tool calls through the gateway will be blocked.
        /// </summary>
        Suspended = 9
    }
}
