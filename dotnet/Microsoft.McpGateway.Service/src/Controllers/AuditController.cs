// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.McpGateway.Management.Contracts.Registry;
using Microsoft.McpGateway.Management.Store.Registry;

namespace Microsoft.McpGateway.Service.Controllers
{
    /// <summary>
    /// Controller for querying audit events.
    /// </summary>
    [ApiController]
    [Route("registry/audit")]
    [Authorize]
    public class AuditController : ControllerBase
    {
        private readonly IAuditEventStore _auditStore;
        private readonly ILogger<AuditController> _logger;

        public AuditController(
            IAuditEventStore auditStore,
            ILogger<AuditController> logger)
        {
            _auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Query audit events with optional filters.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> QueryAuditEvents(
            [FromQuery] DateTimeOffset? startDate,
            [FromQuery] DateTimeOffset? endDate,
            [FromQuery] string? team,
            [FromQuery] string? serverCanonicalId,
            [FromQuery] string? toolName,
            [FromQuery] AuditDecision? decision,
            [FromQuery] string? actor,
            [FromQuery] int limit = 100,
            [FromQuery] int offset = 0,
            CancellationToken cancellationToken = default)
        {
            var filter = new AuditEventFilter
            {
                StartDate = startDate,
                EndDate = endDate,
                Team = team,
                ServerCanonicalId = serverCanonicalId,
                ToolName = toolName,
                Decision = decision,
                Actor = actor,
                Limit = Math.Min(limit, 1000), // Cap at 1000
                Offset = offset
            };

            var events = await _auditStore.QueryAsync(filter, cancellationToken);
            var count = await _auditStore.CountAsync(filter, cancellationToken);

            return Ok(new
            {
                events,
                total = count,
                limit = filter.Limit,
                offset = filter.Offset
            });
        }

        /// <summary>
        /// Get audit event statistics.
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetAuditStats(
            [FromQuery] DateTimeOffset? startDate,
            [FromQuery] DateTimeOffset? endDate,
            CancellationToken cancellationToken)
        {
            var baseFilter = new AuditEventFilter
            {
                StartDate = startDate ?? DateTimeOffset.UtcNow.AddDays(-7),
                EndDate = endDate ?? DateTimeOffset.UtcNow
            };

            var allEvents = await _auditStore.QueryAsync(baseFilter with { Limit = 10000 }, cancellationToken);
            var eventsList = allEvents.ToList();

            var stats = new
            {
                totalEvents = eventsList.Count,
                allowedCount = eventsList.Count(e => e.Decision == AuditDecision.Allowed),
                deniedCount = eventsList.Count(e => e.Decision != AuditDecision.Allowed),
                byDecision = eventsList
                    .GroupBy(e => e.Decision)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                byServer = eventsList
                    .GroupBy(e => e.ServerCanonicalId)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .ToDictionary(g => g.Key, g => g.Count()),
                byTeam = eventsList
                    .Where(e => !string.IsNullOrEmpty(e.Team))
                    .GroupBy(e => e.Team!)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .ToDictionary(g => g.Key, g => g.Count()),
                avgLatencyMs = eventsList.Any() ? eventsList.Average(e => e.LatencyMs) : 0
            };

            return Ok(stats);
        }
    }
}
