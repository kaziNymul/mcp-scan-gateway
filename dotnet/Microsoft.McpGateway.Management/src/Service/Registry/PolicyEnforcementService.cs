// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.McpGateway.Management.Contracts.Registry;
using Microsoft.McpGateway.Management.Store.Registry;

namespace Microsoft.McpGateway.Management.Service.Registry
{
    /// <summary>
    /// Service for enforcing governance policies on tool calls.
    /// </summary>
    public class PolicyEnforcementService : IPolicyEnforcementService
    {
        private readonly IServerRegistryStore _serverStore;
        private readonly IAuditEventStore _auditStore;
        private readonly ILogger<PolicyEnforcementService> _logger;
        private PolicyConfig _policy;

        public PolicyEnforcementService(
            IServerRegistryStore serverStore,
            IAuditEventStore auditStore,
            IOptions<PolicyConfig> policy,
            ILogger<PolicyEnforcementService> logger)
        {
            _serverStore = serverStore ?? throw new ArgumentNullException(nameof(serverStore));
            _auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
            _policy = policy?.Value ?? new PolicyConfig();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PolicyDecision> CheckToolCallAsync(
            string actorId,
            string? actorEmail,
            string? team,
            string serverCanonicalId,
            string toolName,
            CancellationToken cancellationToken)
        {
            // Check bypass principals
            if (_policy.BypassAllowedPrincipals.Contains(actorId, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Actor {ActorId} is in bypass list, allowing", actorId);
                return PolicyDecision.Allow();
            }

            // Check if registry enforcement is enabled
            if (_policy.EnforceRegistryOnly)
            {
                var server = await _serverStore.GetByCanonicalIdAsync(serverCanonicalId, cancellationToken);
                
                if (server == null)
                {
                    return PolicyDecision.Deny(
                        AuditDecision.DeniedServerNotApproved,
                        $"Server '{serverCanonicalId}' is not registered in the governance registry.");
                }

                if (server.Status != ServerStatus.Approved)
                {
                    return PolicyDecision.Deny(
                        AuditDecision.DeniedServerNotApproved,
                        $"Server '{serverCanonicalId}' is not approved. Current status: {server.Status}");
                }

                // Check risk score
                if (server.LatestRiskScore.HasValue && server.LatestRiskScore.Value > _policy.RiskThreshold)
                {
                    if (_policy.RequireAdminForHighRisk)
                    {
                        // TODO: Check if actor is admin
                        // For now, deny high-risk unless bypass
                        return PolicyDecision.Deny(
                            AuditDecision.DeniedHighRisk,
                            $"Server '{serverCanonicalId}' has high risk score ({server.LatestRiskScore:F2}). Admin access required.");
                    }
                }
            }

            // Check global tool denylist
            if (_policy.GlobalToolDenylist.Contains(toolName, StringComparer.OrdinalIgnoreCase))
            {
                return PolicyDecision.Deny(
                    AuditDecision.DeniedToolDenylisted,
                    $"Tool '{toolName}' is on the global denylist.");
            }

            // Check denied tool categories
            foreach (var category in _policy.DeniedToolCategories)
            {
                if (toolName.Contains(category, StringComparison.OrdinalIgnoreCase))
                {
                    return PolicyDecision.Deny(
                        AuditDecision.DeniedToolDenylisted,
                        $"Tool '{toolName}' matches denied category '{category}'.");
                }
            }

            // Check team allowlist if team is specified
            if (!string.IsNullOrEmpty(team) && _policy.TeamAllowlists.TryGetValue(team, out var allowedServers))
            {
                if (allowedServers.Count > 0 && !allowedServers.Contains(serverCanonicalId, StringComparer.OrdinalIgnoreCase))
                {
                    return PolicyDecision.Deny(
                        AuditDecision.DeniedTeamNotAuthorized,
                        $"Team '{team}' is not authorized to use server '{serverCanonicalId}'.");
                }
            }

            // Check team denylist
            if (!string.IsNullOrEmpty(team) && _policy.TeamDenylists.TryGetValue(team, out var deniedServers))
            {
                if (deniedServers.Contains(serverCanonicalId, StringComparer.OrdinalIgnoreCase))
                {
                    return PolicyDecision.Deny(
                        AuditDecision.DeniedTeamNotAuthorized,
                        $"Team '{team}' is denied access to server '{serverCanonicalId}'.");
                }
            }

            // Get server risk score for audit
            double? riskScore = null;
            if (_policy.EnforceRegistryOnly)
            {
                var server = await _serverStore.GetByCanonicalIdAsync(serverCanonicalId, cancellationToken);
                riskScore = server?.LatestRiskScore;
            }

            return PolicyDecision.Allow(riskScore);
        }

        public async Task RecordAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            try
            {
                await _auditStore.CreateAsync(auditEvent, cancellationToken);
            }
            catch (Exception ex)
            {
                // Don't fail the request if audit logging fails, but log the error
                _logger.LogError(ex, "Failed to record audit event {EventId}", auditEvent.Id);
            }
        }

        public Task ReloadPolicyAsync(CancellationToken cancellationToken)
        {
            // In a real implementation, this would reload from ConfigMap or external source
            _logger.LogInformation("Policy configuration reloaded");
            return Task.CompletedTask;
        }
    }
}
