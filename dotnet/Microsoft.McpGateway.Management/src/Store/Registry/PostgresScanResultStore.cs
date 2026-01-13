// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.McpGateway.Management.Contracts.Registry;
using Npgsql;

namespace Microsoft.McpGateway.Management.Store.Registry
{
    /// <summary>
    /// PostgreSQL implementation of the scan result store.
    /// </summary>
    public class PostgresScanResultStore : IScanResultStore
    {
        private readonly string _connectionString;
        private readonly ILogger<PostgresScanResultStore> _logger;

        public PostgresScanResultStore(string connectionString, ILogger<PostgresScanResultStore> logger)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            const string createTableSql = @"
                CREATE TABLE IF NOT EXISTS scans (
                    id UUID PRIMARY KEY,
                    server_id UUID NOT NULL REFERENCES servers(id) ON DELETE CASCADE,
                    server_canonical_id VARCHAR(255) NOT NULL,
                    scanner_version VARCHAR(100) NOT NULL,
                    status INTEGER NOT NULL,
                    risk_score DOUBLE PRECISION,
                    summary TEXT,
                    report_json JSONB,
                    issues JSONB,
                    discovered_tools JSONB,
                    job_name VARCHAR(255),
                    error_message TEXT,
                    started_at TIMESTAMPTZ NOT NULL,
                    finished_at TIMESTAMPTZ,
                    triggered_by VARCHAR(255) NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_scans_server_id ON scans(server_id);
                CREATE INDEX IF NOT EXISTS idx_scans_status ON scans(status);
                CREATE INDEX IF NOT EXISTS idx_scans_started_at ON scans(started_at DESC);
            ";

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(createTableSql, conn);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("PostgreSQL scans table initialized");
        }

        public async Task<ScanResult> CreateAsync(ScanResult scan, CancellationToken cancellationToken)
        {
            const string sql = @"
                INSERT INTO scans (id, server_id, server_canonical_id, scanner_version, status, risk_score,
                    summary, report_json, issues, discovered_tools, job_name, error_message, started_at, 
                    finished_at, triggered_by)
                VALUES (@id, @server_id, @server_canonical_id, @scanner_version, @status, @risk_score,
                    @summary, @report_json::jsonb, @issues::jsonb, @discovered_tools::jsonb, @job_name, 
                    @error_message, @started_at, @finished_at, @triggered_by)
            ";

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            AddScanParameters(cmd, scan);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("Created scan {ScanId} for server {ServerId}", scan.Id, scan.ServerId);
            return scan;
        }

        public async Task<ScanResult?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            const string sql = "SELECT * FROM scans WHERE id = @id";
            
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

        public async Task<ScanResult?> GetLatestByServerIdAsync(Guid serverId, CancellationToken cancellationToken)
        {
            const string sql = "SELECT * FROM scans WHERE server_id = @server_id ORDER BY started_at DESC LIMIT 1";
            
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

        public async Task<ScanResult> UpdateAsync(ScanResult scan, CancellationToken cancellationToken)
        {
            const string sql = @"
                UPDATE scans SET
                    status = @status,
                    risk_score = @risk_score,
                    summary = @summary,
                    report_json = @report_json::jsonb,
                    issues = @issues::jsonb,
                    discovered_tools = @discovered_tools::jsonb,
                    job_name = @job_name,
                    error_message = @error_message,
                    finished_at = @finished_at
                WHERE id = @id
            ";

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", scan.Id);
            cmd.Parameters.AddWithValue("status", (int)scan.Status);
            cmd.Parameters.AddWithValue("risk_score", (object?)scan.RiskScore ?? DBNull.Value);
            cmd.Parameters.AddWithValue("summary", (object?)scan.Summary ?? DBNull.Value);
            cmd.Parameters.AddWithValue("report_json", (object?)scan.ReportJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("issues", JsonSerializer.Serialize(scan.Issues));
            cmd.Parameters.AddWithValue("discovered_tools", JsonSerializer.Serialize(scan.DiscoveredTools));
            cmd.Parameters.AddWithValue("job_name", (object?)scan.JobName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("error_message", (object?)scan.ErrorMessage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("finished_at", (object?)scan.FinishedAt ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("Updated scan {ScanId}", scan.Id);
            return scan;
        }

        public async Task<IEnumerable<ScanResult>> ListByServerIdAsync(Guid serverId, CancellationToken cancellationToken)
        {
            const string sql = "SELECT * FROM scans WHERE server_id = @server_id ORDER BY started_at DESC";
            var results = new List<ScanResult>();
            
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

        public async Task<IEnumerable<ScanResult>> ListByStatusAsync(ScanStatus status, CancellationToken cancellationToken)
        {
            const string sql = "SELECT * FROM scans WHERE status = @status ORDER BY started_at DESC";
            var results = new List<ScanResult>();
            
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("status", (int)status);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(MapFromReader(reader));
            }
            return results;
        }

        private static void AddScanParameters(NpgsqlCommand cmd, ScanResult scan)
        {
            cmd.Parameters.AddWithValue("id", scan.Id);
            cmd.Parameters.AddWithValue("server_id", scan.ServerId);
            cmd.Parameters.AddWithValue("server_canonical_id", scan.ServerCanonicalId);
            cmd.Parameters.AddWithValue("scanner_version", scan.ScannerVersion);
            cmd.Parameters.AddWithValue("status", (int)scan.Status);
            cmd.Parameters.AddWithValue("risk_score", (object?)scan.RiskScore ?? DBNull.Value);
            cmd.Parameters.AddWithValue("summary", (object?)scan.Summary ?? DBNull.Value);
            cmd.Parameters.AddWithValue("report_json", (object?)scan.ReportJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("issues", JsonSerializer.Serialize(scan.Issues));
            cmd.Parameters.AddWithValue("discovered_tools", JsonSerializer.Serialize(scan.DiscoveredTools));
            cmd.Parameters.AddWithValue("job_name", (object?)scan.JobName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("error_message", (object?)scan.ErrorMessage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("started_at", scan.StartedAt);
            cmd.Parameters.AddWithValue("finished_at", (object?)scan.FinishedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("triggered_by", scan.TriggeredBy);
        }

        private static ScanResult MapFromReader(NpgsqlDataReader reader)
        {
            return new ScanResult
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                ServerId = reader.GetGuid(reader.GetOrdinal("server_id")),
                ServerCanonicalId = reader.GetString(reader.GetOrdinal("server_canonical_id")),
                ScannerVersion = reader.GetString(reader.GetOrdinal("scanner_version")),
                Status = (ScanStatus)reader.GetInt32(reader.GetOrdinal("status")),
                RiskScore = reader.IsDBNull(reader.GetOrdinal("risk_score")) ? null : reader.GetDouble(reader.GetOrdinal("risk_score")),
                Summary = reader.IsDBNull(reader.GetOrdinal("summary")) ? null : reader.GetString(reader.GetOrdinal("summary")),
                ReportJson = reader.IsDBNull(reader.GetOrdinal("report_json")) ? null : reader.GetString(reader.GetOrdinal("report_json")),
                Issues = DeserializeJson<List<ScanIssue>>(reader, "issues"),
                DiscoveredTools = DeserializeJson<List<DiscoveredTool>>(reader, "discovered_tools"),
                JobName = reader.IsDBNull(reader.GetOrdinal("job_name")) ? null : reader.GetString(reader.GetOrdinal("job_name")),
                ErrorMessage = reader.IsDBNull(reader.GetOrdinal("error_message")) ? null : reader.GetString(reader.GetOrdinal("error_message")),
                StartedAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("started_at")),
                FinishedAt = reader.IsDBNull(reader.GetOrdinal("finished_at")) ? null : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("finished_at")),
                TriggeredBy = reader.GetString(reader.GetOrdinal("triggered_by"))
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
