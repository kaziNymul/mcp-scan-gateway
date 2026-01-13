// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.McpGateway.Management.Contracts.Registry;
using Microsoft.McpGateway.Management.Extensions;
using Microsoft.McpGateway.Management.Store.Registry;

namespace Microsoft.McpGateway.Management.Service.Registry
{
    /// <summary>
    /// Service for managing server registrations.
    /// </summary>
    public class ServerRegistryService : IServerRegistryService
    {
        private const string CanonicalIdPattern = @"^[a-z0-9][a-z0-9\-_/]*[a-z0-9]$";
        private const string AdminRole = "mcp.admin";

        private readonly IServerRegistryStore _serverStore;
        private readonly IScanResultStore _scanStore;
        private readonly IApprovalStore _approvalStore;
        private readonly IScannerService _scannerService;
        private readonly ILogger<ServerRegistryService> _logger;

        public ServerRegistryService(
            IServerRegistryStore serverStore,
            IScanResultStore scanStore,
            IApprovalStore approvalStore,
            IScannerService scannerService,
            ILogger<ServerRegistryService> logger)
        {
            _serverStore = serverStore ?? throw new ArgumentNullException(nameof(serverStore));
            _scanStore = scanStore ?? throw new ArgumentNullException(nameof(scanStore));
            _approvalStore = approvalStore ?? throw new ArgumentNullException(nameof(approvalStore));
            _scannerService = scannerService ?? throw new ArgumentNullException(nameof(scannerService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ServerRegistration> RegisterAsync(ClaimsPrincipal user, ServerRegistrationRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(user);
            ArgumentNullException.ThrowIfNull(request);

            // Validate canonical ID format
            if (!Regex.IsMatch(request.CanonicalId, CanonicalIdPattern, RegexOptions.IgnoreCase))
            {
                throw new ArgumentException("Canonical ID must contain only lowercase letters, numbers, dashes, underscores, and forward slashes.");
            }

            // Check for duplicate
            var existing = await _serverStore.GetByCanonicalIdAsync(request.CanonicalId, cancellationToken);
            if (existing != null)
            {
                throw new ArgumentException($"A server with canonical ID '{request.CanonicalId}' already exists.");
            }

            var userId = user.GetUserId() ?? throw new InvalidOperationException("User ID not found in claims");
            var registration = ServerRegistration.Create(request, userId);

            _logger.LogInformation("User {UserId} registering server {CanonicalId}", userId, request.CanonicalId);
            
            return await _serverStore.CreateAsync(registration, cancellationToken);
        }

        public async Task<ServerRegistration?> GetAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
        {
            var server = await _serverStore.GetByIdAsync(id, cancellationToken);
            if (server == null) return null;

            if (!CanAccess(user, server))
            {
                throw new UnauthorizedAccessException("Access denied to this server registration.");
            }

            return server;
        }

        public async Task<ServerRegistration?> GetByCanonicalIdAsync(ClaimsPrincipal user, string canonicalId, CancellationToken cancellationToken)
        {
            var server = await _serverStore.GetByCanonicalIdAsync(canonicalId, cancellationToken);
            if (server == null) return null;

            if (!CanAccess(user, server))
            {
                throw new UnauthorizedAccessException("Access denied to this server registration.");
            }

            return server;
        }

        public async Task<IEnumerable<ServerRegistration>> ListAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
        {
            var allServers = await _serverStore.ListAsync(cancellationToken);
            return allServers.Where(s => CanAccess(user, s));
        }

        public async Task<ServerRegistration> UpdateAsync(ClaimsPrincipal user, Guid id, ServerRegistrationRequest request, CancellationToken cancellationToken)
        {
            var existing = await _serverStore.GetByIdAsync(id, cancellationToken)
                ?? throw new ArgumentException("Server not found.");

            if (!CanWrite(user, existing))
            {
                throw new UnauthorizedAccessException("Access denied to update this server registration.");
            }

            // Cannot change canonical ID
            if (!string.Equals(existing.CanonicalId, request.CanonicalId, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Cannot change the canonical ID of an existing registration.");
            }

            // Update fields
            existing.Name = request.Name;
            existing.Description = request.Description;
            existing.OwnerTeam = request.OwnerTeam;
            existing.SourceType = request.SourceType;
            existing.SourceUrl = request.SourceUrl;
            existing.Version = request.Version;
            existing.DeclaredTools = request.DeclaredTools;
            existing.McpConfig = request.McpConfig;
            existing.TestEndpoint = request.TestEndpoint;
            existing.Tags = request.Tags;
            existing.UpdatedAt = DateTimeOffset.UtcNow;

            // If version changed and was previously approved, reset to draft
            if (existing.Status == ServerStatus.Approved)
            {
                existing.Status = ServerStatus.Draft;
                _logger.LogInformation("Server {CanonicalId} version changed, resetting status to Draft", existing.CanonicalId);
            }

            return await _serverStore.UpdateAsync(existing, cancellationToken);
        }

        public async Task DeleteAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
        {
            var existing = await _serverStore.GetByIdAsync(id, cancellationToken)
                ?? throw new ArgumentException("Server not found.");

            if (!CanWrite(user, existing))
            {
                throw new UnauthorizedAccessException("Access denied to delete this server registration.");
            }

            await _serverStore.DeleteAsync(id, cancellationToken);
            _logger.LogInformation("User {UserId} deleted server {CanonicalId}", user.GetUserId(), existing.CanonicalId);
        }

        public async Task<ScanResult> SubmitForScanAsync(ClaimsPrincipal user, Guid serverId, CancellationToken cancellationToken)
        {
            var server = await _serverStore.GetByIdAsync(serverId, cancellationToken)
                ?? throw new ArgumentException("Server not found.");

            if (!CanWrite(user, server))
            {
                throw new UnauthorizedAccessException("Access denied to scan this server.");
            }

            // Update status to pending scan
            server.Status = ServerStatus.PendingScan;
            await _serverStore.UpdateStatusAsync(serverId, ServerStatus.PendingScan, cancellationToken);

            var userId = user.GetUserId() ?? "system";
            _logger.LogInformation("User {UserId} submitting server {CanonicalId} for scan", userId, server.CanonicalId);

            return await _scannerService.TriggerScanAsync(server, userId, cancellationToken);
        }

        public async Task<ScanResult?> GetLatestScanAsync(ClaimsPrincipal user, Guid serverId, CancellationToken cancellationToken)
        {
            var server = await _serverStore.GetByIdAsync(serverId, cancellationToken)
                ?? throw new ArgumentException("Server not found.");

            if (!CanAccess(user, server))
            {
                throw new UnauthorizedAccessException("Access denied to this server.");
            }

            return await _scanStore.GetLatestByServerIdAsync(serverId, cancellationToken);
        }

        public async Task<Approval> ApproveAsync(ClaimsPrincipal user, Guid serverId, ApprovalRequest request, CancellationToken cancellationToken)
        {
            if (!IsAdmin(user))
            {
                throw new UnauthorizedAccessException("Only admins can approve servers.");
            }

            var server = await _serverStore.GetByIdAsync(serverId, cancellationToken)
                ?? throw new ArgumentException("Server not found.");

            // Must have passed scan to be approved
            if (server.Status != ServerStatus.ScannedPass && server.Status != ServerStatus.PendingApproval)
            {
                throw new InvalidOperationException($"Server must pass scanning before approval. Current status: {server.Status}");
            }

            request.Action = ApprovalAction.Approved;
            var approval = Approval.Create(serverId, server.CanonicalId, user.GetUserId()!, request, server.LatestScanId);
            await _approvalStore.CreateAsync(approval, cancellationToken);

            // Update server status
            server.Status = ServerStatus.Approved;
            await _serverStore.UpdateStatusAsync(serverId, ServerStatus.Approved, cancellationToken);

            _logger.LogInformation("Admin {UserId} approved server {CanonicalId}", user.GetUserId(), server.CanonicalId);
            return approval;
        }

        public async Task<Approval> DenyAsync(ClaimsPrincipal user, Guid serverId, ApprovalRequest request, CancellationToken cancellationToken)
        {
            if (!IsAdmin(user))
            {
                throw new UnauthorizedAccessException("Only admins can deny servers.");
            }

            var server = await _serverStore.GetByIdAsync(serverId, cancellationToken)
                ?? throw new ArgumentException("Server not found.");

            request.Action = ApprovalAction.Denied;
            var approval = Approval.Create(serverId, server.CanonicalId, user.GetUserId()!, request, server.LatestScanId);
            await _approvalStore.CreateAsync(approval, cancellationToken);

            // Update server status
            server.Status = ServerStatus.Denied;
            await _serverStore.UpdateStatusAsync(serverId, ServerStatus.Denied, cancellationToken);

            _logger.LogInformation("Admin {UserId} denied server {CanonicalId}: {Reason}", user.GetUserId(), server.CanonicalId, request.Reason);
            return approval;
        }

        public async Task<Approval> SuspendAsync(ClaimsPrincipal user, Guid serverId, ApprovalRequest request, CancellationToken cancellationToken)
        {
            if (!IsAdmin(user))
            {
                throw new UnauthorizedAccessException("Only admins can suspend servers.");
            }

            var server = await _serverStore.GetByIdAsync(serverId, cancellationToken)
                ?? throw new ArgumentException("Server not found.");

            if (server.Status != ServerStatus.Approved)
            {
                throw new InvalidOperationException($"Only approved servers can be suspended. Current status: {server.Status}");
            }

            request.Action = ApprovalAction.Suspended;
            var approval = Approval.Create(serverId, server.CanonicalId, user.GetUserId()!, request, server.LatestScanId);
            await _approvalStore.CreateAsync(approval, cancellationToken);

            await _serverStore.UpdateStatusAsync(serverId, ServerStatus.Suspended, cancellationToken);

            _logger.LogWarning("Admin {UserId} SUSPENDED server {CanonicalId}: {Reason}", user.GetUserId(), server.CanonicalId, request.Reason);
            return approval;
        }

        public async Task<ScanResult> UploadLocalScanAsync(ClaimsPrincipal user, Guid serverId, LocalScanUploadRequest request, CancellationToken cancellationToken)
        {
            var server = await _serverStore.GetByIdAsync(serverId, cancellationToken)
                ?? throw new ArgumentException("Server not found.");

            if (!CanWrite(user, server))
            {
                throw new UnauthorizedAccessException("You do not have permission to upload scans for this server.");
            }

            if (server.SourceType != SourceType.LocalDeclared)
            {
                throw new InvalidOperationException("Local scan upload is only allowed for LocalDeclared servers. Use the /scan endpoint to trigger a remote scan.");
            }

            // Parse the scan output JSON
            ScanResult scanResult;
            try
            {
                scanResult = ParseLocalScanOutput(server, request);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Invalid scan output format: {ex.Message}");
            }

            // Save to database
            await _scanStore.CreateAsync(scanResult, cancellationToken);

            // Update server status based on risk score
            var passThreshold = 50.0; // TODO: Get from config
            var newStatus = scanResult.RiskScore <= passThreshold ? ServerStatus.ScannedPass : ServerStatus.ScannedFail;
            await _serverStore.UpdateStatusAsync(serverId, newStatus, cancellationToken);
            await _serverStore.UpdateLatestScanAsync(serverId, scanResult.Id, scanResult.RiskScore, cancellationToken);

            _logger.LogInformation("User {UserId} uploaded local scan for server {CanonicalId}. Risk score: {RiskScore}", 
                user.GetUserId(), server.CanonicalId, scanResult.RiskScore);

            return scanResult;
        }

        public async Task<IEnumerable<ScanResult>> GetScansAsync(ClaimsPrincipal user, Guid serverId, CancellationToken cancellationToken)
        {
            var server = await _serverStore.GetByIdAsync(serverId, cancellationToken)
                ?? throw new ArgumentException("Server not found.");

            if (!CanAccess(user, server))
            {
                throw new UnauthorizedAccessException("You do not have access to this server.");
            }

            return await _scanStore.GetByServerIdAsync(serverId, cancellationToken);
        }

        public async Task<ScanResult?> GetScanAsync(ClaimsPrincipal user, Guid serverId, Guid scanId, CancellationToken cancellationToken)
        {
            var server = await _serverStore.GetByIdAsync(serverId, cancellationToken)
                ?? throw new ArgumentException("Server not found.");

            if (!CanAccess(user, server))
            {
                throw new UnauthorizedAccessException("You do not have access to this server.");
            }

            return await _scanStore.GetByIdAsync(scanId, cancellationToken);
        }

        private ScanResult ParseLocalScanOutput(ServerRegistration server, LocalScanUploadRequest request)
        {
            // Parse the JSON output from mcp-scan CLI
            using var doc = System.Text.Json.JsonDocument.Parse(request.ScanOutput);
            var root = doc.RootElement;

            var riskScore = root.TryGetProperty("risk_score", out var rs) ? rs.GetDouble() : 0.0;
            
            var issues = new List<ScanIssue>();
            if (root.TryGetProperty("issues", out var issuesArray))
            {
                foreach (var issue in issuesArray.EnumerateArray())
                {
                    issues.Add(new ScanIssue
                    {
                        Severity = issue.TryGetProperty("severity", out var s) ? Enum.Parse<IssueSeverity>(s.GetString()!, true) : IssueSeverity.Info,
                        Category = issue.TryGetProperty("type", out var t) ? t.GetString()! : "unknown",
                        Title = issue.TryGetProperty("message", out var m) ? m.GetString()! : "",
                        Description = issue.TryGetProperty("description", out var d) ? d.GetString() : null,
                    });
                }
            }

            var tools = new List<DiscoveredTool>();
            if (root.TryGetProperty("tools", out var toolsArray))
            {
                foreach (var tool in toolsArray.EnumerateArray())
                {
                    tools.Add(new DiscoveredTool
                    {
                        Name = tool.TryGetProperty("name", out var n) ? n.GetString()! : "unknown",
                        Description = tool.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                    });
                }
            }

            return new ScanResult
            {
                Id = Guid.NewGuid(),
                ServerId = server.Id,
                ServerCanonicalId = server.CanonicalId,
                ScannerVersion = request.ScanVersion,
                Status = ScanStatus.Completed,
                RiskScore = riskScore,
                StartedAt = DateTime.TryParse(request.ScannedAt, out var scanned) ? scanned : DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                ReportJson = request.ScanOutput,
                Issues = issues,
                DiscoveredTools = tools,
            };
        }

        public async Task<bool> IsApprovedAsync(string canonicalId, CancellationToken cancellationToken)
        {
            var server = await _serverStore.GetByCanonicalIdAsync(canonicalId, cancellationToken);
            return server?.Status == ServerStatus.Approved;
        }

        private bool CanAccess(ClaimsPrincipal user, ServerRegistration server)
        {
            if (IsAdmin(user)) return true;
            
            var userId = user.GetUserId();
            if (string.Equals(userId, server.CreatedBy, StringComparison.OrdinalIgnoreCase)) return true;

            var userTeams = user.GetUserRoles();
            return userTeams.Contains(server.OwnerTeam, StringComparer.OrdinalIgnoreCase);
        }

        private bool CanWrite(ClaimsPrincipal user, ServerRegistration server)
        {
            if (IsAdmin(user)) return true;
            
            var userId = user.GetUserId();
            return string.Equals(userId, server.CreatedBy, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAdmin(ClaimsPrincipal user)
        {
            var roles = user.GetUserRoles();
            return roles.Any(r => string.Equals(r, AdminRole, StringComparison.OrdinalIgnoreCase));
        }
    }
}
