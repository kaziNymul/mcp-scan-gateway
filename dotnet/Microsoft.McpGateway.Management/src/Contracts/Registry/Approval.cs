// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.McpGateway.Management.Contracts.Registry
{
    /// <summary>
    /// Type of approval action.
    /// </summary>
    public enum ApprovalAction
    {
        /// <summary>
        /// Server was approved for use.
        /// </summary>
        Approved = 0,

        /// <summary>
        /// Server approval was denied.
        /// </summary>
        Denied = 1,

        /// <summary>
        /// Server was deprecated.
        /// </summary>
        Deprecated = 2,

        /// <summary>
        /// Server was suspended.
        /// </summary>
        Suspended = 3,

        /// <summary>
        /// Server was reinstated.
        /// </summary>
        Reinstated = 4,

        /// <summary>
        /// Approval was revoked.
        /// </summary>
        Revoked = 5
    }

    /// <summary>
    /// Request to approve or deny a server.
    /// </summary>
    public class ApprovalRequest
    {
        /// <summary>
        /// Action to take: approve, deny, deprecate, suspend, etc.
        /// </summary>
        [JsonPropertyName("action")]
        public required ApprovalAction Action { get; set; }

        /// <summary>
        /// Reason for the action.
        /// </summary>
        [JsonPropertyName("reason")]
        public required string Reason { get; set; }

        /// <summary>
        /// Optional conditions or notes.
        /// </summary>
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        /// <summary>
        /// Optional expiration date for time-limited approvals.
        /// </summary>
        [JsonPropertyName("expiresAt")]
        public DateTimeOffset? ExpiresAt { get; set; }
    }

    /// <summary>
    /// Represents an approval record in the registry.
    /// </summary>
    public class Approval
    {
        /// <summary>
        /// Unique identifier for this approval record.
        /// </summary>
        [JsonPropertyName("id")]
        public required Guid Id { get; set; }

        /// <summary>
        /// ID of the server this approval is for.
        /// </summary>
        [JsonPropertyName("serverId")]
        public required Guid ServerId { get; set; }

        /// <summary>
        /// Canonical ID of the server.
        /// </summary>
        [JsonPropertyName("serverCanonicalId")]
        public required string ServerCanonicalId { get; set; }

        /// <summary>
        /// User who made this approval decision.
        /// </summary>
        [JsonPropertyName("actor")]
        public required string Actor { get; set; }

        /// <summary>
        /// Action taken.
        /// </summary>
        [JsonPropertyName("action")]
        public required ApprovalAction Action { get; set; }

        /// <summary>
        /// Reason for the action.
        /// </summary>
        [JsonPropertyName("reason")]
        public required string Reason { get; set; }

        /// <summary>
        /// Additional notes.
        /// </summary>
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        /// <summary>
        /// When this approval was made.
        /// </summary>
        [JsonPropertyName("timestamp")]
        public required DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Optional expiration for the approval.
        /// </summary>
        [JsonPropertyName("expiresAt")]
        public DateTimeOffset? ExpiresAt { get; set; }

        /// <summary>
        /// ID of the scan that was reviewed (if any).
        /// </summary>
        [JsonPropertyName("scanId")]
        public Guid? ScanId { get; set; }

        /// <summary>
        /// Creates a new approval record.
        /// </summary>
        public static Approval Create(Guid serverId, string serverCanonicalId, string actor, ApprovalRequest request, Guid? scanId = null)
        {
            return new Approval
            {
                Id = Guid.NewGuid(),
                ServerId = serverId,
                ServerCanonicalId = serverCanonicalId,
                Actor = actor,
                Action = request.Action,
                Reason = request.Reason,
                Notes = request.Notes,
                Timestamp = DateTimeOffset.UtcNow,
                ExpiresAt = request.ExpiresAt,
                ScanId = scanId
            };
        }
    }
}
