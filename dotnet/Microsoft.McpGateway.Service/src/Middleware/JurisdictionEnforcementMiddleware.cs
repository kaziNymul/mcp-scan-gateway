// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.McpGateway.Management.Contracts.Registry;
using Microsoft.McpGateway.Management.Extensions;
using Microsoft.McpGateway.Management.Service.Registry;

namespace Microsoft.McpGateway.Service.Middleware
{
    /// <summary>
    /// Middleware that enforces MCP governance policies on tool calls.
    /// </summary>
    public class JurisdictionEnforcementMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IPolicyEnforcementService _policyService;
        private readonly JurisdictionConfig _config;
        private readonly ILogger<JurisdictionEnforcementMiddleware> _logger;

        public JurisdictionEnforcementMiddleware(
            RequestDelegate next,
            IPolicyEnforcementService policyService,
            IOptions<JurisdictionConfig> config,
            ILogger<JurisdictionEnforcementMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _policyService = policyService ?? throw new ArgumentNullException(nameof(policyService));
            _config = config?.Value ?? new JurisdictionConfig();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip if jurisdiction is disabled
            if (!_config.Enabled)
            {
                await _next(context);
                return;
            }

            // Only apply to MCP endpoints
            var path = context.Request.Path.Value ?? "";
            if (!IsMcpEndpoint(path))
            {
                await _next(context);
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            var traceId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

            try
            {
                // Extract call information
                var (serverCanonicalId, toolName) = await ExtractCallInfoAsync(context, path);
                
                if (string.IsNullOrEmpty(serverCanonicalId) || string.IsNullOrEmpty(toolName))
                {
                    // Can't determine call info, let it through but log
                    _logger.LogDebug("Could not extract MCP call info from request");
                    await _next(context);
                    return;
                }

                // Get actor info from claims
                var actorId = context.User?.GetUserId() ?? "anonymous";
                var actorEmail = context.User?.FindFirst("email")?.Value;
                var team = context.User?.FindFirst("team")?.Value ?? context.User?.FindFirst("groups")?.Value;

                // Check policy
                var decision = await _policyService.CheckToolCallAsync(
                    actorId, actorEmail, team, serverCanonicalId, toolName,
                    context.RequestAborted);

                stopwatch.Stop();

                // Create audit event
                var auditEvent = new AuditEvent
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTimeOffset.UtcNow,
                    Actor = actorId,
                    ActorEmail = actorEmail,
                    Team = team,
                    ServerCanonicalId = serverCanonicalId,
                    ToolName = toolName,
                    Decision = decision.Decision,
                    Reason = decision.Reason,
                    LatencyMs = stopwatch.ElapsedMilliseconds,
                    RequestSize = context.Request.ContentLength ?? 0,
                    TraceId = traceId,
                    SourceIp = context.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = context.Request.Headers.UserAgent.ToString(),
                    ServerRiskScore = decision.ServerRiskScore
                };

                if (decision.Allowed)
                {
                    // Allow the request
                    await _next(context);
                    
                    // Update response size in audit event
                    auditEvent.ResponseSize = context.Response.ContentLength ?? 0;
                }
                else
                {
                    // Deny the request
                    if (_config.EnforcementMode == EnforcementMode.Enforce)
                    {
                        _logger.LogWarning(
                            "Blocked tool call: actor={Actor}, server={Server}, tool={Tool}, reason={Reason}",
                            actorId, serverCanonicalId, toolName, decision.Reason);

                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(JsonSerializer.Serialize(new
                        {
                            error = "Access denied by governance policy",
                            reason = decision.Reason,
                            decision = decision.Decision.ToString(),
                            serverCanonicalId,
                            toolName,
                            traceId
                        }));
                    }
                    else
                    {
                        // Audit mode - log but allow
                        _logger.LogInformation(
                            "AUDIT: Would block tool call: actor={Actor}, server={Server}, tool={Tool}, reason={Reason}",
                            actorId, serverCanonicalId, toolName, decision.Reason);
                        await _next(context);
                    }
                }

                // Record audit event (fire and forget)
                _ = _policyService.RecordAuditEventAsync(auditEvent, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in jurisdiction enforcement middleware");
                
                // On error, fail open or closed based on config
                if (_config.EnforcementMode == EnforcementMode.Enforce)
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        error = "Policy enforcement error",
                        traceId
                    }));
                }
                else
                {
                    await _next(context);
                }
            }
        }

        private static bool IsMcpEndpoint(string path)
        {
            return path.EndsWith("/mcp", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("/adapters/", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("/tools/", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<(string? serverCanonicalId, string? toolName)> ExtractCallInfoAsync(HttpContext context, string path)
        {
            string? serverCanonicalId = null;
            string? toolName = null;

            // Try to extract from path
            // e.g., /adapters/{name}/mcp
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2 && segments[0].Equals("adapters", StringComparison.OrdinalIgnoreCase))
            {
                serverCanonicalId = segments[1];
            }

            // For tool name, we might need to parse the request body
            // MCP requests typically have a method name
            if (context.Request.ContentLength > 0 && context.Request.ContentType?.Contains("json") == true)
            {
                context.Request.EnableBuffering();
                try
                {
                    using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                    var body = await reader.ReadToEndAsync();
                    context.Request.Body.Position = 0;

                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("method", out var methodProp))
                    {
                        var method = methodProp.GetString();
                        if (method == "tools/call" && doc.RootElement.TryGetProperty("params", out var paramsProp))
                        {
                            if (paramsProp.TryGetProperty("name", out var nameProp))
                            {
                                toolName = nameProp.GetString();
                            }
                        }
                        else
                        {
                            toolName = method;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to parse request body for tool name");
                }
            }

            return (serverCanonicalId, toolName ?? "unknown");
        }
    }

    public static class JurisdictionEnforcementMiddlewareExtensions
    {
        public static IApplicationBuilder UseJurisdictionEnforcement(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<JurisdictionEnforcementMiddleware>();
        }
    }
}
