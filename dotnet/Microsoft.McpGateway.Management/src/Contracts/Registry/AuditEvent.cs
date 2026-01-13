// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.McpGateway.Management.Contracts.Registry
{
    /// <summary>
    /// Decision made for an audit event.
    /// </summary>
    public enum AuditDecision
    {
        /// <summary>
        /// Request was allowed.
        /// </summary>
        Allowed = 0,

        /// <summary>
        /// Request was denied due to server not being approved.
        /// </summary>
        DeniedServerNotApproved = 1,

        /// <summary>
        /// Request was denied due to tool being on the denylist.
        /// </summary>
        DeniedToolDenylisted = 2,

        /// <summary>
        /// Request was denied due to team not being authorized.
        /// </summary>
        DeniedTeamNotAuthorized = 3,

        /// <summary>
        /// Request was denied due to high risk score.
        /// </summary>
        DeniedHighRisk = 4,

        /// <summary>
        /// Request was denied due to rate limiting.
        /// </summary>
        DeniedRateLimited = 5,

        /// <summary>
        /// Request was denied due to payload size.
        /// </summary>
        DeniedPayloadTooLarge = 6,

        /// <summary>
        /// Request timed out.
        /// </summary>
        TimedOut = 7,

        /// <summary>
        /// An error occurred during processing.
        /// </summary>
        Error = 8
    }

    /// <summary>
    /// Represents an audit event for tool invocations through the gateway.
    /// </summary>
    public class AuditEvent
    {
        /// <summary>
        /// Unique identifier for this audit event.
        /// </summary>
        [JsonPropertyName("id")]
        public required Guid Id { get; set; }

        /// <summary>
        /// When this event occurred.
        /// </summary>
        [JsonPropertyName("timestamp")]
        public required DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// User or service account that made the request.
        /// </summary>
        [JsonPropertyName("actor")]
        public required string Actor { get; set; }

        /// <summary>
        /// Email of the actor, if available.
        /// </summary>
        [JsonPropertyName("actorEmail")]
        public string? ActorEmail { get; set; }

        /// <summary>
        /// Team of the actor (from JWT claims).
        /// </summary>
        [JsonPropertyName("team")]
        public string? Team { get; set; }

        /// <summary>
        /// Canonical ID of the server being accessed.
        /// </summary>
        [JsonPropertyName("serverCanonicalId")]
        public required string ServerCanonicalId { get; set; }

        /// <summary>
        /// Name of the tool being invoked.
        /// </summary>
        [JsonPropertyName("toolName")]
        public required string ToolName { get; set; }

        /// <summary>
        /// Decision made by the gateway.
        /// </summary>
        [JsonPropertyName("decision")]
        public required AuditDecision Decision { get; set; }

        /// <summary>
        /// Reason for the decision, especially if denied.
        /// </summary>
        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        /// <summary>
        /// Latency in milliseconds for processing this request.
        /// </summary>
        [JsonPropertyName("latencyMs")]
        public long LatencyMs { get; set; }

        /// <summary>
        /// Size of the request payload in bytes.
        /// </summary>
        [JsonPropertyName("requestSize")]
        public long RequestSize { get; set; }

        /// <summary>
        /// Size of the response payload in bytes.
        /// </summary>
        [JsonPropertyName("responseSize")]
        public long ResponseSize { get; set; }

        /// <summary>
        /// Distributed trace ID for correlation.
        /// </summary>
        [JsonPropertyName("traceId")]
        public string? TraceId { get; set; }

        /// <summary>
        /// Source IP address of the request.
        /// </summary>
        [JsonPropertyName("sourceIp")]
        public string? SourceIp { get; set; }

        /// <summary>
        /// User agent of the client.
        /// </summary>
        [JsonPropertyName("userAgent")]
        public string? UserAgent { get; set; }

        /// <summary>
        /// Risk score of the server at the time of invocation.
        /// </summary>
        [JsonPropertyName("serverRiskScore")]
        public double? ServerRiskScore { get; set; }

        /// <summary>
        /// Creates a new audit event.
        /// </summary>
        public static AuditEvent Create(
            string actor,
            string serverCanonicalId,
            string toolName,
            AuditDecision decision,
            string? reason = null)
        {
            return new AuditEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                Actor = actor,
                ServerCanonicalId = serverCanonicalId,
                ToolName = toolName,
                Decision = decision,
                Reason = reason
            };
        }
    }

    /// <summary>
    /// Filter criteria for querying audit events.
    /// </summary>
    public class AuditEventFilter
    {
        /// <summary>
        /// Start of date range.
        /// </summary>
        [JsonPropertyName("startDate")]
        public DateTimeOffset? StartDate { get; set; }

        /// <summary>
        /// End of date range.
        /// </summary>
        [JsonPropertyName("endDate")]
        public DateTimeOffset? EndDate { get; set; }

        /// <summary>
        /// Filter by team.
        /// </summary>
        [JsonPropertyName("team")]
        public string? Team { get; set; }

        /// <summary>
        /// Filter by server canonical ID.
        /// </summary>
        [JsonPropertyName("serverCanonicalId")]
        public string? ServerCanonicalId { get; set; }

        /// <summary>
        /// Filter by tool name.
        /// </summary>
        [JsonPropertyName("toolName")]
        public string? ToolName { get; set; }

        /// <summary>
        /// Filter by decision type.
        /// </summary>
        [JsonPropertyName("decision")]
        public AuditDecision? Decision { get; set; }

        /// <summary>
        /// Filter by actor.
        /// </summary>
        [JsonPropertyName("actor")]
        public string? Actor { get; set; }

        /// <summary>
        /// Maximum number of results to return.
        /// </summary>
        [JsonPropertyName("limit")]
        public int Limit { get; set; } = 100;

        /// <summary>
        /// Offset for pagination.
        /// </summary>
        [JsonPropertyName("offset")]
        public int Offset { get; set; } = 0;
    }
}
