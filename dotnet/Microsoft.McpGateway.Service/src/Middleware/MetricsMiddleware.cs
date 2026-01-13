// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Prometheus;

namespace Microsoft.McpGateway.Service.Middleware
{
    /// <summary>
    /// Middleware for exposing Prometheus metrics.
    /// </summary>
    public static class MetricsMiddleware
    {
        // Counters
        public static readonly Counter ToolCallsAllowed = Metrics.CreateCounter(
            "mcp_tool_calls_allowed_total",
            "Total number of tool calls that were allowed",
            new CounterConfiguration
            {
                LabelNames = new[] { "server", "tool", "team" }
            });

        public static readonly Counter ToolCallsDenied = Metrics.CreateCounter(
            "mcp_tool_calls_denied_total",
            "Total number of tool calls that were denied",
            new CounterConfiguration
            {
                LabelNames = new[] { "server", "tool", "team", "reason" }
            });

        public static readonly Counter ScanRunsTotal = Metrics.CreateCounter(
            "mcp_scan_runs_total",
            "Total number of security scans run",
            new CounterConfiguration
            {
                LabelNames = new[] { "status" }
            });

        public static readonly Counter ServerRegistrations = Metrics.CreateCounter(
            "mcp_server_registrations_total",
            "Total number of server registrations",
            new CounterConfiguration
            {
                LabelNames = new[] { "source_type", "status" }
            });

        // Gauges
        public static readonly Gauge ApprovedServersCount = Metrics.CreateGauge(
            "mcp_approved_servers_count",
            "Number of currently approved servers");

        public static readonly Gauge PendingScansCount = Metrics.CreateGauge(
            "mcp_pending_scans_count",
            "Number of scans currently pending or running");

        // Histograms
        public static readonly Histogram ScanRiskScore = Metrics.CreateHistogram(
            "mcp_scan_risk_score",
            "Distribution of scan risk scores",
            new HistogramConfiguration
            {
                Buckets = new[] { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0 }
            });

        public static readonly Histogram ToolCallLatency = Metrics.CreateHistogram(
            "mcp_tool_call_latency_seconds",
            "Latency of tool calls",
            new HistogramConfiguration
            {
                LabelNames = new[] { "server", "tool" },
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 15) // 1ms to ~16s
            });

        public static readonly Histogram PolicyCheckLatency = Metrics.CreateHistogram(
            "mcp_policy_check_latency_seconds",
            "Latency of policy checks",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(0.0001, 2, 12) // 0.1ms to ~200ms
            });

        /// <summary>
        /// Records a tool call metric.
        /// </summary>
        public static void RecordToolCall(string server, string tool, string team, bool allowed, string? denyReason = null)
        {
            if (allowed)
            {
                ToolCallsAllowed.WithLabels(server, tool, team ?? "unknown").Inc();
            }
            else
            {
                ToolCallsDenied.WithLabels(server, tool, team ?? "unknown", denyReason ?? "unknown").Inc();
            }
        }

        /// <summary>
        /// Records a scan run metric.
        /// </summary>
        public static void RecordScanRun(string status, double? riskScore = null)
        {
            ScanRunsTotal.WithLabels(status).Inc();
            
            if (riskScore.HasValue)
            {
                ScanRiskScore.Observe(riskScore.Value);
            }
        }

        /// <summary>
        /// Extension method to add Prometheus metrics endpoint.
        /// </summary>
        public static IApplicationBuilder UsePrometheusMetrics(this IApplicationBuilder app)
        {
            // Use the default Prometheus /metrics endpoint
            app.UseMetricServer("/metrics");
            
            // Add HTTP request metrics
            app.UseHttpMetrics();

            return app;
        }
    }
}
