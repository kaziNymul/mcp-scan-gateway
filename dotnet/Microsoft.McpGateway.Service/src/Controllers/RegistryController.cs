// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.McpGateway.Management.Contracts.Registry;
using Microsoft.McpGateway.Management.Service.Registry;

namespace Microsoft.McpGateway.Service.Controllers
{
    /// <summary>
    /// Controller for managing server registrations in the governance registry.
    /// </summary>
    [ApiController]
    [Route("registry/servers")]
    [Authorize]
    public class RegistryController : ControllerBase
    {
        private readonly IServerRegistryService _registryService;
        private readonly ILogger<RegistryController> _logger;

        public RegistryController(
            IServerRegistryService registryService,
            ILogger<RegistryController> logger)
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Register a new MCP server in the governance registry.
        /// </summary>
        /// <remarks>
        /// This endpoint allows users to submit a new MCP server for governance review.
        /// The server will be created in DRAFT status and must be scanned and approved
        /// before it can be used through the gateway.
        /// </remarks>
        [HttpPost]
        public async Task<IActionResult> RegisterServer([FromBody] ServerRegistrationRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _registryService.RegisterAsync(HttpContext.User, request, cancellationToken);
                return CreatedAtAction(nameof(GetServer), new { id = result.Id }, result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// List all servers in the registry that the user can access.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ListServers(CancellationToken cancellationToken)
        {
            var servers = await _registryService.ListAsync(HttpContext.User, cancellationToken);
            return Ok(servers);
        }

        /// <summary>
        /// Get a specific server registration by ID.
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetServer(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var server = await _registryService.GetAsync(HttpContext.User, id, cancellationToken);
                if (server == null)
                    return NotFound(new { error = "Server not found" });
                return Ok(server);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        /// <summary>
        /// Get a specific server registration by canonical ID.
        /// </summary>
        [HttpGet("by-canonical-id/{canonicalId}")]
        public async Task<IActionResult> GetServerByCanonicalId(string canonicalId, CancellationToken cancellationToken)
        {
            try
            {
                var server = await _registryService.GetByCanonicalIdAsync(HttpContext.User, canonicalId, cancellationToken);
                if (server == null)
                    return NotFound(new { error = "Server not found" });
                return Ok(server);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        /// <summary>
        /// Update a server registration.
        /// </summary>
        /// <remarks>
        /// Note: If a server was previously approved, updating it will reset its status to DRAFT,
        /// requiring re-scanning and re-approval.
        /// </remarks>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateServer(Guid id, [FromBody] ServerRegistrationRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _registryService.UpdateAsync(HttpContext.User, id, request, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        /// <summary>
        /// Delete a server registration.
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteServer(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                await _registryService.DeleteAsync(HttpContext.User, id, cancellationToken);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        /// <summary>
        /// Submit a server for security scanning.
        /// </summary>
        /// <remarks>
        /// This triggers an MCP-Scan job that will analyze the server's source code,
        /// declared tools, and optionally connect to a test endpoint to verify tool signatures.
        /// </remarks>
        [HttpPost("{id:guid}/scan")]
        public async Task<IActionResult> ScanServer(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _registryService.SubmitForScanAsync(HttpContext.User, id, cancellationToken);
                return Accepted(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        /// <summary>
        /// Get the latest scan result for a server.
        /// </summary>
        [HttpGet("{id:guid}/scan/latest")]
        public async Task<IActionResult> GetLatestScan(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _registryService.GetLatestScanAsync(HttpContext.User, id, cancellationToken);
                if (result == null)
                    return NotFound(new { error = "No scan results found" });
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        /// <summary>
        /// Approve a server for use (admin only).
        /// </summary>
        /// <remarks>
        /// Only servers that have passed scanning can be approved.
        /// Once approved, the server's tools can be invoked through the gateway.
        /// </remarks>
        [HttpPost("{id:guid}/approve")]
        public async Task<IActionResult> ApproveServer(Guid id, [FromBody] ApprovalRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _registryService.ApproveAsync(HttpContext.User, id, request, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        /// <summary>
        /// Deny a server (admin only).
        /// </summary>
        [HttpPost("{id:guid}/deny")]
        public async Task<IActionResult> DenyServer(Guid id, [FromBody] ApprovalRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _registryService.DenyAsync(HttpContext.User, id, request, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        /// <summary>
        /// Upload local scan results for a server.
        /// </summary>
        /// <remarks>
        /// For servers running on developer machines (LocalDeclared source type),
        /// users must run mcp-scan locally and upload the JSON results using this endpoint.
        /// This is required because the K8s scanner job cannot reach local servers.
        /// </remarks>
        [HttpPost("{id:guid}/scan/upload")]
        public async Task<IActionResult> UploadLocalScan(Guid id, [FromBody] LocalScanUploadRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _registryService.UploadLocalScanAsync(HttpContext.User, id, request, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        /// <summary>
        /// Suspend a server (admin only).
        /// </summary>
        /// <remarks>
        /// Suspending a server immediately blocks all tool invocations through the gateway.
        /// Use this for emergency security responses.
        /// </remarks>
        [HttpPost("{id:guid}/suspend")]
        public async Task<IActionResult> SuspendServer(Guid id, [FromBody] ApprovalRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _registryService.SuspendAsync(HttpContext.User, id, request, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        /// <summary>
        /// Get all scan results for a server.
        /// </summary>
        [HttpGet("{id:guid}/scans")]
        public async Task<IActionResult> GetScans(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var results = await _registryService.GetScansAsync(HttpContext.User, id, cancellationToken);
                return Ok(results);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        /// <summary>
        /// Get a specific scan result.
        /// </summary>
        [HttpGet("{id:guid}/scans/{scanId:guid}")]
        public async Task<IActionResult> GetScan(Guid id, Guid scanId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _registryService.GetScanAsync(HttpContext.User, id, scanId, cancellationToken);
                if (result == null)
                    return NotFound(new { error = "Scan not found" });
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }
    }
}

