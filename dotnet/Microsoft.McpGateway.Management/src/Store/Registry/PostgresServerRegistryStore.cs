// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.McpGateway.Management.Contracts.Registry;
using Npgsql;

namespace Microsoft.McpGateway.Management.Store.Registry
{
    /// <summary>
    /// PostgreSQL implementation of the server registry store.
    /// </summary>
    public class PostgresServerRegistryStore : IServerRegistryStore
    {
        private readonly string _connectionString;
        private readonly ILogger<PostgresServerRegistryStore> _logger;

        public PostgresServerRegistryStore(string connectionString, ILogger<PostgresServerRegistryStore> logger)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            const string createTableSql = @"
                CREATE TABLE IF NOT EXISTS servers (
                    id UUID PRIMARY KEY,
                    canonical_id VARCHAR(255) NOT NULL UNIQUE,
                    name VARCHAR(255) NOT NULL,
                    description TEXT,
                    owner_team VARCHAR(255) NOT NULL,
                    source_type INTEGER NOT NULL,
                    source_url TEXT,
                    version VARCHAR(100) NOT NULL,
                    status INTEGER NOT NULL,
                    declared_tools JSONB,
                    mcp_config JSONB,
                    test_endpoint TEXT,
                    tags JSONB,
                    created_by VARCHAR(255) NOT NULL,
                    created_at TIMESTAMPTZ NOT NULL,
                    updated_at TIMESTAMPTZ NOT NULL,
                    latest_scan_id UUID,
                    latest_risk_score DOUBLE PRECISION
                );

                CREATE INDEX IF NOT EXISTS idx_servers_canonical_id ON servers(canonical_id);
                CREATE INDEX IF NOT EXISTS idx_servers_status ON servers(status);
                CREATE INDEX IF NOT EXISTS idx_servers_owner_team ON servers(owner_team);
            ";

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(createTableSql, conn);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("PostgreSQL servers table initialized");
        }

        public async Task<ServerRegistration> CreateAsync(ServerRegistration registration, CancellationToken cancellationToken)
        {
            const string sql = @"
                INSERT INTO servers (id, canonical_id, name, description, owner_team, source_type, source_url, 
                    version, status, declared_tools, mcp_config, test_endpoint, tags, created_by, created_at, updated_at)
                VALUES (@id, @canonical_id, @name, @description, @owner_team, @source_type, @source_url,
                    @version, @status, @declared_tools::jsonb, @mcp_config::jsonb, @test_endpoint, @tags::jsonb, 
                    @created_by, @created_at, @updated_at)
            ";

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            
            cmd.Parameters.AddWithValue("id", registration.Id);
            cmd.Parameters.AddWithValue("canonical_id", registration.CanonicalId);
            cmd.Parameters.AddWithValue("name", registration.Name);
            cmd.Parameters.AddWithValue("description", (object?)registration.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("owner_team", registration.OwnerTeam);
            cmd.Parameters.AddWithValue("source_type", (int)registration.SourceType);
            cmd.Parameters.AddWithValue("source_url", (object?)registration.SourceUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("version", registration.Version);
            cmd.Parameters.AddWithValue("status", (int)registration.Status);
            cmd.Parameters.AddWithValue("declared_tools", JsonSerializer.Serialize(registration.DeclaredTools));
            cmd.Parameters.AddWithValue("mcp_config", JsonSerializer.Serialize(registration.McpConfig));
            cmd.Parameters.AddWithValue("test_endpoint", (object?)registration.TestEndpoint ?? DBNull.Value);
            cmd.Parameters.AddWithValue("tags", JsonSerializer.Serialize(registration.Tags));
            cmd.Parameters.AddWithValue("created_by", registration.CreatedBy);
            cmd.Parameters.AddWithValue("created_at", registration.CreatedAt);
            cmd.Parameters.AddWithValue("updated_at", registration.UpdatedAt);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("Created server registration {CanonicalId}", registration.CanonicalId);
            return registration;
        }

        public async Task<ServerRegistration?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            const string sql = "SELECT * FROM servers WHERE id = @id";
            
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

        public async Task<ServerRegistration?> GetByCanonicalIdAsync(string canonicalId, CancellationToken cancellationToken)
        {
            const string sql = "SELECT * FROM servers WHERE canonical_id = @canonical_id";
            
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("canonical_id", canonicalId);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return MapFromReader(reader);
            }
            return null;
        }

        public async Task<ServerRegistration> UpdateAsync(ServerRegistration registration, CancellationToken cancellationToken)
        {
            const string sql = @"
                UPDATE servers SET
                    name = @name,
                    description = @description,
                    owner_team = @owner_team,
                    source_type = @source_type,
                    source_url = @source_url,
                    version = @version,
                    status = @status,
                    declared_tools = @declared_tools::jsonb,
                    mcp_config = @mcp_config::jsonb,
                    test_endpoint = @test_endpoint,
                    tags = @tags::jsonb,
                    updated_at = @updated_at,
                    latest_scan_id = @latest_scan_id,
                    latest_risk_score = @latest_risk_score
                WHERE id = @id
            ";

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            
            cmd.Parameters.AddWithValue("id", registration.Id);
            cmd.Parameters.AddWithValue("name", registration.Name);
            cmd.Parameters.AddWithValue("description", (object?)registration.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("owner_team", registration.OwnerTeam);
            cmd.Parameters.AddWithValue("source_type", (int)registration.SourceType);
            cmd.Parameters.AddWithValue("source_url", (object?)registration.SourceUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("version", registration.Version);
            cmd.Parameters.AddWithValue("status", (int)registration.Status);
            cmd.Parameters.AddWithValue("declared_tools", JsonSerializer.Serialize(registration.DeclaredTools));
            cmd.Parameters.AddWithValue("mcp_config", JsonSerializer.Serialize(registration.McpConfig));
            cmd.Parameters.AddWithValue("test_endpoint", (object?)registration.TestEndpoint ?? DBNull.Value);
            cmd.Parameters.AddWithValue("tags", JsonSerializer.Serialize(registration.Tags));
            cmd.Parameters.AddWithValue("updated_at", DateTimeOffset.UtcNow);
            cmd.Parameters.AddWithValue("latest_scan_id", (object?)registration.LatestScanId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("latest_risk_score", (object?)registration.LatestRiskScore ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("Updated server registration {CanonicalId}", registration.CanonicalId);
            return registration;
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            const string sql = "DELETE FROM servers WHERE id = @id";
            
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("Deleted server registration {Id}", id);
        }

        public async Task<IEnumerable<ServerRegistration>> ListAsync(CancellationToken cancellationToken)
        {
            const string sql = "SELECT * FROM servers ORDER BY created_at DESC";
            return await ExecuteQueryAsync(sql, null, cancellationToken);
        }

        public async Task<IEnumerable<ServerRegistration>> ListByStatusAsync(ServerStatus status, CancellationToken cancellationToken)
        {
            const string sql = "SELECT * FROM servers WHERE status = @status ORDER BY created_at DESC";
            return await ExecuteQueryAsync(sql, cmd => cmd.Parameters.AddWithValue("status", (int)status), cancellationToken);
        }

        public async Task<IEnumerable<ServerRegistration>> ListByTeamAsync(string ownerTeam, CancellationToken cancellationToken)
        {
            const string sql = "SELECT * FROM servers WHERE owner_team = @owner_team ORDER BY created_at DESC";
            return await ExecuteQueryAsync(sql, cmd => cmd.Parameters.AddWithValue("owner_team", ownerTeam), cancellationToken);
        }

        public async Task UpdateStatusAsync(Guid id, ServerStatus status, CancellationToken cancellationToken)
        {
            const string sql = "UPDATE servers SET status = @status, updated_at = @updated_at WHERE id = @id";
            
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("status", (int)status);
            cmd.Parameters.AddWithValue("updated_at", DateTimeOffset.UtcNow);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task UpdateLatestScanAsync(Guid id, Guid scanId, double riskScore, CancellationToken cancellationToken)
        {
            const string sql = @"
                UPDATE servers SET 
                    latest_scan_id = @scan_id, 
                    latest_risk_score = @risk_score,
                    updated_at = @updated_at 
                WHERE id = @id";
            
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("scan_id", scanId);
            cmd.Parameters.AddWithValue("risk_score", riskScore);
            cmd.Parameters.AddWithValue("updated_at", DateTimeOffset.UtcNow);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task<IEnumerable<ServerRegistration>> ExecuteQueryAsync(
            string sql, 
            Action<NpgsqlCommand>? parameterizer, 
            CancellationToken cancellationToken)
        {
            var results = new List<ServerRegistration>();
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            parameterizer?.Invoke(cmd);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(MapFromReader(reader));
            }
            return results;
        }

        private static ServerRegistration MapFromReader(NpgsqlDataReader reader)
        {
            return new ServerRegistration
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                CanonicalId = reader.GetString(reader.GetOrdinal("canonical_id")),
                Name = reader.GetString(reader.GetOrdinal("name")),
                Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description")),
                OwnerTeam = reader.GetString(reader.GetOrdinal("owner_team")),
                SourceType = (SourceType)reader.GetInt32(reader.GetOrdinal("source_type")),
                SourceUrl = reader.IsDBNull(reader.GetOrdinal("source_url")) ? null : reader.GetString(reader.GetOrdinal("source_url")),
                Version = reader.GetString(reader.GetOrdinal("version")),
                Status = (ServerStatus)reader.GetInt32(reader.GetOrdinal("status")),
                DeclaredTools = DeserializeJson<List<DeclaredTool>>(reader, "declared_tools"),
                McpConfig = DeserializeJson<McpServerConfig>(reader, "mcp_config"),
                TestEndpoint = reader.IsDBNull(reader.GetOrdinal("test_endpoint")) ? null : reader.GetString(reader.GetOrdinal("test_endpoint")),
                Tags = DeserializeJson<List<string>>(reader, "tags"),
                CreatedBy = reader.GetString(reader.GetOrdinal("created_by")),
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")),
                UpdatedAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("updated_at")),
                LatestScanId = reader.IsDBNull(reader.GetOrdinal("latest_scan_id")) ? null : reader.GetGuid(reader.GetOrdinal("latest_scan_id")),
                LatestRiskScore = reader.IsDBNull(reader.GetOrdinal("latest_risk_score")) ? null : reader.GetDouble(reader.GetOrdinal("latest_risk_score"))
            };
        }

        private static T? DeserializeJson<T>(NpgsqlDataReader reader, string column) where T : class
        {
            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return null;
            var json = reader.GetString(ordinal);
            if (string.IsNullOrEmpty(json) || json == "null") return null;
            return JsonSerializer.Deserialize<T>(json);
        }
    }
}
