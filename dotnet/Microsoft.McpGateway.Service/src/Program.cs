// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Identity;
using k8s;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Identity.Web;
using Microsoft.McpGateway.Management.Authorization;
using Microsoft.McpGateway.Management.Contracts.Registry;
using Microsoft.McpGateway.Management.Deployment;
using Microsoft.McpGateway.Management.Service;
using Microsoft.McpGateway.Management.Service.Registry;
using Microsoft.McpGateway.Management.Store;
using Microsoft.McpGateway.Management.Store.Registry;
using Microsoft.McpGateway.Service.Authentication;
using Microsoft.McpGateway.Service.Middleware;
using Microsoft.McpGateway.Service.Routing;
using Microsoft.McpGateway.Service.Session;
using ModelContextProtocol.AspNetCore.Authentication;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var credential = new DefaultAzureCredential();

builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddLogging();

builder.Services.AddSingleton<IKubernetesClientFactory, LocalKubernetesClientFactory>();
builder.Services.AddSingleton<IAdapterSessionStore, DistributedMemorySessionStore>();
builder.Services.AddSingleton<IServiceNodeInfoProvider, AdapterKubernetesNodeInfoProvider>();
builder.Services.AddSingleton<ISessionRoutingHandler, AdapterSessionRoutingHandler>();

// ============================================================================
// Jurisdiction Configuration
// ============================================================================
var jurisdictionConfig = builder.Configuration.GetSection("Jurisdiction").Get<JurisdictionConfig>() ?? new JurisdictionConfig();
builder.Services.Configure<JurisdictionConfig>(builder.Configuration.GetSection("Jurisdiction"));
builder.Services.Configure<PolicyConfig>(builder.Configuration.GetSection("Jurisdiction:Policy"));
builder.Services.Configure<ScannerConfig>(builder.Configuration.GetSection("Jurisdiction:Scanner"));

// PostgreSQL connection for registry
var postgresConnection = jurisdictionConfig.PostgresConnection 
    ?? builder.Configuration.GetValue<string>("PostgresConnection")
    ?? "Host=postgres-service;Database=mcpgov;Username=mcpgov;Password=mcpgov";

// Register PostgreSQL stores
builder.Services.AddSingleton<IServerRegistryStore>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<PostgresServerRegistryStore>>();
    return new PostgresServerRegistryStore(postgresConnection, logger);
});

builder.Services.AddSingleton<IScanResultStore>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<PostgresScanResultStore>>();
    return new PostgresScanResultStore(postgresConnection, logger);
});

builder.Services.AddSingleton<IApprovalStore>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<PostgresApprovalStore>>();
    return new PostgresApprovalStore(postgresConnection, logger);
});

builder.Services.AddSingleton<IAuditEventStore>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<PostgresAuditEventStore>>();
    return new PostgresAuditEventStore(postgresConnection, logger);
});

// Register Kubernetes client for scanner jobs
builder.Services.AddSingleton<IKubernetes>(sp =>
{
    var config = KubernetesClientConfiguration.InClusterConfig();
    return new Kubernetes(config);
});

// Register scanner service
builder.Services.AddSingleton<IScannerService, KubernetesScannerService>();

// Register registry service
builder.Services.AddSingleton<IServerRegistryService, ServerRegistryService>();

// Register policy enforcement service
builder.Services.AddSingleton<IPolicyEnforcementService, PolicyEnforcementService>();

// ============================================================================
// Authentication Configuration
// ============================================================================
if (builder.Environment.IsDevelopment())
{
    builder.Services
        .AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(DevelopmentAuthenticationHandler.SchemeName, null);

    var redisConnection = builder.Configuration.GetValue<string>("Redis:ConnectionString") ?? "localhost:6379";
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "mcpgateway:";
    });

    builder.Services.AddSingleton<IAdapterResourceStore, RedisAdapterResourceStore>();
    builder.Services.AddSingleton<IToolResourceStore, RedisToolResourceStore>();

    builder.Logging.AddConsole();
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
}
else
{
    var azureAdConfig = builder.Configuration.GetSection("AzureAd");
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddScheme<McpAuthenticationOptions, McpSubPathAwareAuthenticationHandler>(
        McpAuthenticationDefaults.AuthenticationScheme,
        McpAuthenticationDefaults.DisplayName,
    options =>
    {
        options.ResourceMetadata = new()
        {
            Resource = new Uri(builder.Configuration.GetValue<string>("PublicOrigin")!),
            AuthorizationServers = { new Uri($"https://login.microsoftonline.com/{azureAdConfig["TenantId"]}/v2.0") },
            ScopesSupported = [$"api://{azureAdConfig["ClientId"]}/.default"]
        };
    })
    .AddMicrosoftIdentityWebApi(azureAdConfig);

    // Create CosmosClient with credential-based authentication
    var cosmosConfig = builder.Configuration.GetSection("CosmosSettings");
    var cosmosClient = new CosmosClient(
        cosmosConfig["AccountEndpoint"], 
        credential, 
        new CosmosClientOptions
        {
            Serializer = new CosmosSystemTextJsonSerializer(new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            })
        });

    builder.Services.AddSingleton<IAdapterResourceStore>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<CosmosAdapterResourceStore>>();
        return new CosmosAdapterResourceStore(cosmosClient, cosmosConfig["DatabaseName"]!, "AdapterContainer", logger);
    });

    builder.Services.AddSingleton<IToolResourceStore>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<CosmosToolResourceStore>>();
        return new CosmosToolResourceStore(cosmosClient, cosmosConfig["DatabaseName"]!, "ToolContainer", logger);
    });
    
    builder.Services.AddCosmosCache(options =>
    {
        options.ContainerName = "CacheContainer";
        options.DatabaseName = cosmosConfig["DatabaseName"]!;
        options.CreateIfNotExists = true;
        options.ClientBuilder = new CosmosClientBuilder(cosmosConfig["AccountEndpoint"], credential);
    });
}

// ============================================================================
// Core Services
// ============================================================================
builder.Services.AddSingleton<IKubeClientWrapper>(c =>
{
    var kubeClientFactory = c.GetRequiredService<IKubernetesClientFactory>();
    return new KubeClient(kubeClientFactory, "adapter");
});
builder.Services.AddSingleton<IPermissionProvider, SimplePermissionProvider>();
builder.Services.AddSingleton<IAdapterDeploymentManager>(c =>
{
    var config = builder.Configuration.GetSection("ContainerRegistrySettings");
    return new KubernetesAdapterDeploymentManager(config["Endpoint"]!, c.GetRequiredService<IKubeClientWrapper>(), c.GetRequiredService<ILogger<KubernetesAdapterDeploymentManager>>());
});
builder.Services.AddSingleton<IAdapterManagementService, AdapterManagementService>();
builder.Services.AddSingleton<IToolManagementService, ToolManagementService>();
builder.Services.AddSingleton<IAdapterRichResultProvider, AdapterRichResultProvider>();

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8000);
});

var app = builder.Build();

// ============================================================================
// Initialize PostgreSQL tables
// ============================================================================
using (var scope = app.Services.CreateScope())
{
    var serverStore = scope.ServiceProvider.GetRequiredService<IServerRegistryStore>() as PostgresServerRegistryStore;
    var scanStore = scope.ServiceProvider.GetRequiredService<IScanResultStore>() as PostgresScanResultStore;
    var approvalStore = scope.ServiceProvider.GetRequiredService<IApprovalStore>() as PostgresApprovalStore;
    var auditStore = scope.ServiceProvider.GetRequiredService<IAuditEventStore>() as PostgresAuditEventStore;

    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        if (serverStore != null) await serverStore.InitializeAsync(CancellationToken.None);
        if (scanStore != null) await scanStore.InitializeAsync(CancellationToken.None);
        if (approvalStore != null) await approvalStore.InitializeAsync(CancellationToken.None);
        if (auditStore != null) await auditStore.InitializeAsync(CancellationToken.None);
        logger.LogInformation("PostgreSQL tables initialized successfully");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to initialize PostgreSQL tables - will retry on first use");
    }
}

// ============================================================================
// Configure HTTP Request Pipeline
// ============================================================================

// Prometheus metrics endpoint
app.UsePrometheusMetrics();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Jurisdiction enforcement middleware (for MCP endpoints)
if (jurisdictionConfig.Enabled)
{
    app.UseJurisdictionEnforcement();
}

// Map controllers
app.MapControllers();

await app.RunAsync();
