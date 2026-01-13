// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.McpGateway.Management.Contracts.Registry;

namespace Microsoft.McpGateway.Management.Store.Registry
{
    /// <summary>
    /// Interface for storing and retrieving server registrations.
    /// </summary>
    public interface IServerRegistryStore
    {
        /// <summary>
        /// Creates a new server registration.
        /// </summary>
        Task<ServerRegistration> CreateAsync(ServerRegistration registration, CancellationToken cancellationToken);

        /// <summary>
        /// Gets a server registration by ID.
        /// </summary>
        Task<ServerRegistration?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// Gets a server registration by canonical ID.
        /// </summary>
        Task<ServerRegistration?> GetByCanonicalIdAsync(string canonicalId, CancellationToken cancellationToken);

        /// <summary>
        /// Updates an existing server registration.
        /// </summary>
        Task<ServerRegistration> UpdateAsync(ServerRegistration registration, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes a server registration.
        /// </summary>
        Task DeleteAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// Lists all server registrations.
        /// </summary>
        Task<IEnumerable<ServerRegistration>> ListAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Lists server registrations by status.
        /// </summary>
        Task<IEnumerable<ServerRegistration>> ListByStatusAsync(ServerStatus status, CancellationToken cancellationToken);

        /// <summary>
        /// Lists server registrations by owner team.
        /// </summary>
        Task<IEnumerable<ServerRegistration>> ListByTeamAsync(string ownerTeam, CancellationToken cancellationToken);

        /// <summary>
        /// Updates the status of a server.
        /// </summary>
        Task UpdateStatusAsync(Guid id, ServerStatus status, CancellationToken cancellationToken);

        /// <summary>
        /// Updates the latest scan information for a server.
        /// </summary>
        Task UpdateLatestScanAsync(Guid id, Guid scanId, double riskScore, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Interface for storing and retrieving scan results.
    /// </summary>
    public interface IScanResultStore
    {
        /// <summary>
        /// Creates a new scan result.
        /// </summary>
        Task<ScanResult> CreateAsync(ScanResult scan, CancellationToken cancellationToken);

        /// <summary>
        /// Gets a scan result by ID.
        /// </summary>
        Task<ScanResult?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the latest scan result for a server.
        /// </summary>
        Task<ScanResult?> GetLatestByServerIdAsync(Guid serverId, CancellationToken cancellationToken);

        /// <summary>
        /// Updates an existing scan result.
        /// </summary>
        Task<ScanResult> UpdateAsync(ScanResult scan, CancellationToken cancellationToken);

        /// <summary>
        /// Lists all scan results for a server.
        /// </summary>
        Task<IEnumerable<ScanResult>> ListByServerIdAsync(Guid serverId, CancellationToken cancellationToken);

        /// <summary>
        /// Lists scans by status.
        /// </summary>
        Task<IEnumerable<ScanResult>> ListByStatusAsync(ScanStatus status, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Interface for storing and retrieving approval records.
    /// </summary>
    public interface IApprovalStore
    {
        /// <summary>
        /// Creates a new approval record.
        /// </summary>
        Task<Approval> CreateAsync(Approval approval, CancellationToken cancellationToken);

        /// <summary>
        /// Gets an approval by ID.
        /// </summary>
        Task<Approval?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the latest approval for a server.
        /// </summary>
        Task<Approval?> GetLatestByServerIdAsync(Guid serverId, CancellationToken cancellationToken);

        /// <summary>
        /// Lists all approvals for a server.
        /// </summary>
        Task<IEnumerable<Approval>> ListByServerIdAsync(Guid serverId, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Interface for storing and retrieving audit events.
    /// </summary>
    public interface IAuditEventStore
    {
        /// <summary>
        /// Creates a new audit event.
        /// </summary>
        Task CreateAsync(AuditEvent auditEvent, CancellationToken cancellationToken);

        /// <summary>
        /// Creates multiple audit events in batch.
        /// </summary>
        Task CreateBatchAsync(IEnumerable<AuditEvent> auditEvents, CancellationToken cancellationToken);

        /// <summary>
        /// Queries audit events with filters.
        /// </summary>
        Task<IEnumerable<AuditEvent>> QueryAsync(AuditEventFilter filter, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the count of audit events matching the filter.
        /// </summary>
        Task<long> CountAsync(AuditEventFilter filter, CancellationToken cancellationToken);
    }
}
