// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.McpGateway.Management.Contracts.Registry;
using Npgsql;

namespace Microsoft.McpGateway.Management.Store.Registry
{
    /// <summary>
    /// PostgreSQL implementation of the approval store.
    /// </summary>
    public class PostgresApprovalStore : IApprovalStore
    {
        private readonly string _connectionString;
        private readonly ILogger<PostgresApprovalStore> _logger;

        public PostgresApprovalStore(string connectionString, ILogger<PostgresApprovalStore> logger)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            const string createTableSql = @"
                CREATE TABLE IF NOT EXISTS approvals (
                    id UUID PRIMARY KEY,
                    server_id UUID NOT NULL REFERENCES servers(id) ON DELETE CASCADE,
                    server_canonical_id VARCHAR(255) NOT NULL,
                    actor VARCHAR(255) NOT NULL,
                    action INTEGER NOT NULL,
                    reason TEXT NOT NULL,
                    notes TEXT,
                    timestamp TIMESTAMPTZ NOT NULL,
                    expires_at TIMESTAMPTZ,
                    scan_id UUID
                );

                CREATE INDEX IF NOT EXISTS idx_approvals_server_id ON approvals(server_id);
                CREATE INDEX IF NOT EXISTS idx_approvals_timestamp ON approvals(timestamp DESC);
            ";

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(createTableSql, conn);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("PostgreSQL approvals table initialized");
        }

        public async Task<Approval> CreateAsync(Approval approval, CancellationToken cancellationToken)
        {
            const string sql = @"
                INSERT INTO approvals (id, server_id, server_canonical_id, actor, action, reason, notes, 
                    timestamp, expires_at, scan_id)
                VALUES (@id, @server_id, @server_canonical_id, @actor, @action, @reason, @notes, 
                    @timestamp, @expires_at, @scan_id)
            ";

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            
            cmd.Parameters.AddWithValue("id", approval.Id);
            cmd.Parameters.AddWithValue("server_id", approval.ServerId);
            cmd.Parameters.AddWithValue("server_canonical_id", approval.ServerCanonicalId);
            cmd.Parameters.AddWithValue("actor", approval.Actor);
            cmd.Parameters.AddWithValue("action", (int)approval.Action);
            cmd.Parameters.AddWithValue("reason", approval.Reason);
            cmd.Parameters.AddWithValue("notes", (object?)approval.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("timestamp", approval.Timestamp);
            cmd.Parameters.AddWithValue("expires_at", (object?)approval.ExpiresAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("scan_id", (object?)approval.ScanId ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("Created approval {ApprovalId} for server {ServerId}", approval.Id, approval.ServerId);
            return approval;
        }

        public async Task<Approval?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            const string sql = "SELECT * FROM approvals WHERE id = @id";
            
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return MapFromReader(reader);
            }
            return null;
        }

        public async Task<Approval?> GetLatestByServerIdAsync(Guid serverId, CancellationToken cancellationToken)
        {
            const string sql = "SELECT * FROM approvals WHERE server_id = @server_id ORDER BY timestamp DESC LIMIT 1";
            
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("server_id", serverId);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return MapFromReader(reader);
            }
            return null;
        }

        public async Task<IEnumerable<Approval>> ListByServerIdAsync(Guid serverId, CancellationToken cancellationToken)
        {
            const string sql = "SELECT * FROM approvals WHERE server_id = @server_id ORDER BY timestamp DESC";
            var results = new List<Approval>();
            
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("server_id", serverId);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(MapFromReader(reader));
            }
            return results;
        }

        private static Approval MapFromReader(NpgsqlDataReader reader)
        {
            return new Approval
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                ServerId = reader.GetGuid(reader.GetOrdinal("server_id")),
                ServerCanonicalId = reader.GetString(reader.GetOrdinal("server_canonical_id")),
                Actor = reader.GetString(reader.GetOrdinal("actor")),
                Action = (ApprovalAction)reader.GetInt32(reader.GetOrdinal("action")),
                Reason = reader.GetString(reader.GetOrdinal("reason")),
                Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes")),
                Timestamp = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("timestamp")),
                ExpiresAt = reader.IsDBNull(reader.GetOrdinal("expires_at")) ? null : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("expires_at")),
                ScanId = reader.IsDBNull(reader.GetOrdinal("scan_id")) ? null : reader.GetGuid(reader.GetOrdinal("scan_id"))
            };
        }
    }
}
