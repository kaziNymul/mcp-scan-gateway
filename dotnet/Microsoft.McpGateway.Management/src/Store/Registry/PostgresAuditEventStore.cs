// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.McpGateway.Management.Contracts.Registry;
using Npgsql;

namespace Microsoft.McpGateway.Management.Store.Registry
{
    /// <summary>
    /// PostgreSQL implementation of the audit event store.
    /// </summary>
    public class PostgresAuditEventStore : IAuditEventStore
    {
        private readonly string _connectionString;
        private readonly ILogger<PostgresAuditEventStore> _logger;

        public PostgresAuditEventStore(string connectionString, ILogger<PostgresAuditEventStore> logger)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            const string createTableSql = @"
                CREATE TABLE IF NOT EXISTS audit_events (
                    id UUID PRIMARY KEY,
                    timestamp TIMESTAMPTZ NOT NULL,
                    actor VARCHAR(255) NOT NULL,
                    actor_email VARCHAR(255),
                    team VARCHAR(255),
                    server_canonical_id VARCHAR(255) NOT NULL,
                    tool_name VARCHAR(255) NOT NULL,
                    decision INTEGER NOT NULL,
                    reason TEXT,
                    latency_ms BIGINT NOT NULL DEFAULT 0,
                    request_size BIGINT NOT NULL DEFAULT 0,
                    response_size BIGINT NOT NULL DEFAULT 0,
                    trace_id VARCHAR(64),
                    source_ip VARCHAR(45),
                    user_agent TEXT,
                    server_risk_score DOUBLE PRECISION
                );

                CREATE INDEX IF NOT EXISTS idx_audit_timestamp ON audit_events(timestamp DESC);
                CREATE INDEX IF NOT EXISTS idx_audit_actor ON audit_events(actor);
                CREATE INDEX IF NOT EXISTS idx_audit_team ON audit_events(team);
                CREATE INDEX IF NOT EXISTS idx_audit_server ON audit_events(server_canonical_id);
                CREATE INDEX IF NOT EXISTS idx_audit_decision ON audit_events(decision);
            ";

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(createTableSql, conn);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("PostgreSQL audit_events table initialized");
        }

        public async Task CreateAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            const string sql = @"
                INSERT INTO audit_events (id, timestamp, actor, actor_email, team, server_canonical_id, 
                    tool_name, decision, reason, latency_ms, request_size, response_size, trace_id, 
                    source_ip, user_agent, server_risk_score)
                VALUES (@id, @timestamp, @actor, @actor_email, @team, @server_canonical_id, 
                    @tool_name, @decision, @reason, @latency_ms, @request_size, @response_size, @trace_id, 
                    @source_ip, @user_agent, @server_risk_score)
            ";

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            AddEventParameters(cmd, auditEvent);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task CreateBatchAsync(IEnumerable<AuditEvent> auditEvents, CancellationToken cancellationToken)
        {
            if (!auditEvents.Any()) return;

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            foreach (var auditEvent in auditEvents)
            {
                const string sql = @"
                    INSERT INTO audit_events (id, timestamp, actor, actor_email, team, server_canonical_id, 
                        tool_name, decision, reason, latency_ms, request_size, response_size, trace_id, 
                        source_ip, user_agent, server_risk_score)
                    VALUES (@id, @timestamp, @actor, @actor_email, @team, @server_canonical_id, 
                        @tool_name, @decision, @reason, @latency_ms, @request_size, @response_size, @trace_id, 
                        @source_ip, @user_agent, @server_risk_score)
                ";
                await using var cmd = new NpgsqlCommand(sql, conn);
                AddEventParameters(cmd, auditEvent);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            _logger.LogDebug("Inserted {Count} audit events", auditEvents.Count());
        }

        public async Task<IEnumerable<AuditEvent>> QueryAsync(AuditEventFilter filter, CancellationToken cancellationToken)
        {
            var (sql, parameters) = BuildQuery(filter, selectCount: false);
            var results = new List<AuditEvent>();

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            foreach (var (name, value) in parameters)
            {
                cmd.Parameters.AddWithValue(name, value);
            }

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(MapFromReader(reader));
            }
            return results;
        }

        public async Task<long> CountAsync(AuditEventFilter filter, CancellationToken cancellationToken)
        {
            var (sql, parameters) = BuildQuery(filter, selectCount: true);

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            foreach (var (name, value) in parameters)
            {
                cmd.Parameters.AddWithValue(name, value);
            }

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return result != null ? Convert.ToInt64(result) : 0;
        }

        private static (string sql, List<(string name, object value)> parameters) BuildQuery(AuditEventFilter filter, bool selectCount)
        {
            var parameters = new List<(string name, object value)>();
            var whereClause = new StringBuilder("WHERE 1=1");

            if (filter.StartDate.HasValue)
            {
                whereClause.Append(" AND timestamp >= @start_date");
                parameters.Add(("start_date", filter.StartDate.Value));
            }

            if (filter.EndDate.HasValue)
            {
                whereClause.Append(" AND timestamp <= @end_date");
                parameters.Add(("end_date", filter.EndDate.Value));
            }

            if (!string.IsNullOrEmpty(filter.Team))
            {
                whereClause.Append(" AND team = @team");
                parameters.Add(("team", filter.Team));
            }

            if (!string.IsNullOrEmpty(filter.ServerCanonicalId))
            {
                whereClause.Append(" AND server_canonical_id = @server_canonical_id");
                parameters.Add(("server_canonical_id", filter.ServerCanonicalId));
            }

            if (!string.IsNullOrEmpty(filter.ToolName))
            {
                whereClause.Append(" AND tool_name = @tool_name");
                parameters.Add(("tool_name", filter.ToolName));
            }

            if (filter.Decision.HasValue)
            {
                whereClause.Append(" AND decision = @decision");
                parameters.Add(("decision", (int)filter.Decision.Value));
            }

            if (!string.IsNullOrEmpty(filter.Actor))
            {
                whereClause.Append(" AND actor = @actor");
                parameters.Add(("actor", filter.Actor));
            }

            string sql;
            if (selectCount)
            {
                sql = $"SELECT COUNT(*) FROM audit_events {whereClause}";
            }
            else
            {
                sql = $"SELECT * FROM audit_events {whereClause} ORDER BY timestamp DESC LIMIT @limit OFFSET @offset";
                parameters.Add(("limit", filter.Limit));
                parameters.Add(("offset", filter.Offset));
            }

            return (sql, parameters);
        }

        private static void AddEventParameters(NpgsqlCommand cmd, AuditEvent e)
        {
            cmd.Parameters.AddWithValue("id", e.Id);
            cmd.Parameters.AddWithValue("timestamp", e.Timestamp);
            cmd.Parameters.AddWithValue("actor", e.Actor);
            cmd.Parameters.AddWithValue("actor_email", (object?)e.ActorEmail ?? DBNull.Value);
            cmd.Parameters.AddWithValue("team", (object?)e.Team ?? DBNull.Value);
            cmd.Parameters.AddWithValue("server_canonical_id", e.ServerCanonicalId);
            cmd.Parameters.AddWithValue("tool_name", e.ToolName);
            cmd.Parameters.AddWithValue("decision", (int)e.Decision);
            cmd.Parameters.AddWithValue("reason", (object?)e.Reason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("latency_ms", e.LatencyMs);
            cmd.Parameters.AddWithValue("request_size", e.RequestSize);
            cmd.Parameters.AddWithValue("response_size", e.ResponseSize);
            cmd.Parameters.AddWithValue("trace_id", (object?)e.TraceId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("source_ip", (object?)e.SourceIp ?? DBNull.Value);
            cmd.Parameters.AddWithValue("user_agent", (object?)e.UserAgent ?? DBNull.Value);
            cmd.Parameters.AddWithValue("server_risk_score", (object?)e.ServerRiskScore ?? DBNull.Value);
        }

        private static AuditEvent MapFromReader(NpgsqlDataReader reader)
        {
            return new AuditEvent
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                Timestamp = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("timestamp")),
                Actor = reader.GetString(reader.GetOrdinal("actor")),
                ActorEmail = reader.IsDBNull(reader.GetOrdinal("actor_email")) ? null : reader.GetString(reader.GetOrdinal("actor_email")),
                Team = reader.IsDBNull(reader.GetOrdinal("team")) ? null : reader.GetString(reader.GetOrdinal("team")),
                ServerCanonicalId = reader.GetString(reader.GetOrdinal("server_canonical_id")),
                ToolName = reader.GetString(reader.GetOrdinal("tool_name")),
                Decision = (AuditDecision)reader.GetInt32(reader.GetOrdinal("decision")),
                Reason = reader.IsDBNull(reader.GetOrdinal("reason")) ? null : reader.GetString(reader.GetOrdinal("reason")),
                LatencyMs = reader.GetInt64(reader.GetOrdinal("latency_ms")),
                RequestSize = reader.GetInt64(reader.GetOrdinal("request_size")),
                ResponseSize = reader.GetInt64(reader.GetOrdinal("response_size")),
                TraceId = reader.IsDBNull(reader.GetOrdinal("trace_id")) ? null : reader.GetString(reader.GetOrdinal("trace_id")),
                SourceIp = reader.IsDBNull(reader.GetOrdinal("source_ip")) ? null : reader.GetString(reader.GetOrdinal("source_ip")),
                UserAgent = reader.IsDBNull(reader.GetOrdinal("user_agent")) ? null : reader.GetString(reader.GetOrdinal("user_agent")),
                ServerRiskScore = reader.IsDBNull(reader.GetOrdinal("server_risk_score")) ? null : reader.GetDouble(reader.GetOrdinal("server_risk_score"))
            };
        }
    }
}
