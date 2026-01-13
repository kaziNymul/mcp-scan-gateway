// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Claims;
using Microsoft.McpGateway.Management.Contracts.Registry;

namespace Microsoft.McpGateway.Management.Service.Registry
{
    /// <summary>
    /// Interface for managing server registrations.
    /// </summary>
    public interface IServerRegistryService
    {
        /// <summary>
        /// Registers a new MCP server.
        /// </summary>
        Task<ServerRegistration> RegisterAsync(ClaimsPrincipal user, ServerRegistrationRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// Gets a server registration by ID.
        /// </summary>
        Task<ServerRegistration?> GetAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// Gets a server registration by canonical ID.
        /// </summary>
        Task<ServerRegistration?> GetByCanonicalIdAsync(ClaimsPrincipal user, string canonicalId, CancellationToken cancellationToken);

        /// <summary>
        /// Lists all server registrations the user can access.
        /// </summary>
        Task<IEnumerable<ServerRegistration>> ListAsync(ClaimsPrincipal user, CancellationToken cancellationToken);

        /// <summary>
        /// Updates a server registration.
        /// </summary>
        Task<ServerRegistration> UpdateAsync(ClaimsPrincipal user, Guid id, ServerRegistrationRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes a server registration.
        /// </summary>
        Task DeleteAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// Submits a server for scanning.
        /// </summary>
        Task<ScanResult> SubmitForScanAsync(ClaimsPrincipal user, Guid serverId, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the latest scan result for a server.
        /// </summary>
        Task<ScanResult?> GetLatestScanAsync(ClaimsPrincipal user, Guid serverId, CancellationToken cancellationToken);

        /// <summary>
        /// Approves a server.
        /// </summary>
        Task<Approval> ApproveAsync(ClaimsPrincipal user, Guid serverId, ApprovalRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// Denies a server.
        /// </summary>
        Task<Approval> DenyAsync(ClaimsPrincipal user, Guid serverId, ApprovalRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// Suspends an approved server.
        /// </summary>
        Task<Approval> SuspendAsync(ClaimsPrincipal user, Guid serverId, ApprovalRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// Uploads local scan results for a server.
        /// </summary>
        Task<ScanResult> UploadLocalScanAsync(ClaimsPrincipal user, Guid serverId, LocalScanUploadRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// Gets all scan results for a server.
        /// </summary>
        Task<IEnumerable<ScanResult>> GetScansAsync(ClaimsPrincipal user, Guid serverId, CancellationToken cancellationToken);

        /// <summary>
        /// Gets a specific scan result.
        /// </summary>
        Task<ScanResult?> GetScanAsync(ClaimsPrincipal user, Guid serverId, Guid scanId, CancellationToken cancellationToken);

        /// <summary>
        /// Checks if a server is approved and usable.
        /// </summary>
        Task<bool> IsApprovedAsync(string canonicalId, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Interface for the scanner service that runs MCP-Scan.
    /// </summary>
    public interface IScannerService
    {
        /// <summary>
        /// Triggers a scan for a server registration.
        /// </summary>
        Task<ScanResult> TriggerScanAsync(ServerRegistration server, string triggeredBy, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the status of an ongoing scan.
        /// </summary>
        Task<ScanResult?> GetScanStatusAsync(Guid scanId, CancellationToken cancellationToken);

        /// <summary>
        /// Processes completed scan jobs (called by background worker).
        /// </summary>
        Task ProcessCompletedScansAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Cancels an ongoing scan.
        /// </summary>
        Task CancelScanAsync(Guid scanId, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Interface for policy enforcement.
    /// </summary>
    public interface IPolicyEnforcementService
    {
        /// <summary>
        /// Checks if a tool call is allowed.
        /// </summary>
        Task<PolicyDecision> CheckToolCallAsync(
            string actorId,
            string? actorEmail,
            string? team,
            string serverCanonicalId,
            string toolName,
            CancellationToken cancellationToken);

        /// <summary>
        /// Records an audit event.
        /// </summary>
        Task RecordAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken);

        /// <summary>
        /// Reloads policy configuration.
        /// </summary>
        Task ReloadPolicyAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// Result of a policy check.
    /// </summary>
    public class PolicyDecision
    {
        /// <summary>
        /// Whether the call is allowed.
        /// </summary>
        public bool Allowed { get; set; }

        /// <summary>
        /// Decision type.
        /// </summary>
        public AuditDecision Decision { get; set; }

        /// <summary>
        /// Reason for the decision.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Risk score of the server.
        /// </summary>
        public double? ServerRiskScore { get; set; }

        /// <summary>
        /// Creates an allowed decision.
        /// </summary>
        public static PolicyDecision Allow(double? riskScore = null) => new()
        {
            Allowed = true,
            Decision = AuditDecision.Allowed,
            ServerRiskScore = riskScore
        };

        /// <summary>
        /// Creates a denied decision.
        /// </summary>
        public static PolicyDecision Deny(AuditDecision decision, string reason) => new()
        {
            Allowed = false,
            Decision = decision,
            Reason = reason
        };
    }
}
