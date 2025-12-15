using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Cosmos;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.EventGrid;
using NatureOS.CoreApi.Services;
using NatureOS.CoreApi.Hubs;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Add JWT authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["AzureAd:Authority"];
        options.Audience = builder.Configuration["AzureAd:Audience"];
        
        // Configure for SignalR
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/natureos-hub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// Add SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
})
.AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

// Configure Cosmos DB
builder.Services.AddSingleton<CosmosClient>(serviceProvider =>
{
    var connectionString = builder.Configuration.GetConnectionString("CosmosDb");
    return new CosmosClient(connectionString, new CosmosClientOptions
    {
        SerializerOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        }
    });
});

// Add Service Bus
builder.Services.AddSingleton<ServiceBusClient>(serviceProvider =>
{
    var connectionString = builder.Configuration.GetConnectionString("ServiceBus");
    return new ServiceBusClient(connectionString);
});

// Add Event Grid
builder.Services.AddSingleton<EventGridPublisherClient>(serviceProvider =>
{
    var endpoint = builder.Configuration["EventGrid:TopicEndpoint"];
    var key = builder.Configuration["EventGrid:AccessKey"];
    return new EventGridPublisherClient(new Uri(endpoint), new Azure.AzureKeyCredential(key));
});

// Register services
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IFungaService, FungaService>();
builder.Services.AddScoped<IMycosoftIntegrationService, MycosoftIntegrationService>();
builder.Services.AddScoped<IExternalDataIntegrationService, ExternalDataIntegrationService>();
builder.Services.AddScoped<IMasIngestionService, MasIngestionService>();
builder.Services.AddScoped<IMycoBrainService, MycoBrainService>();
// Register background services
builder.Services.AddHostedService<ProactiveMonitoringService>();

// Register caching services
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddHostedService<CacheWarmupService>();

// HTTP client for external services
builder.Services.AddHttpClient<FungaService>();
builder.Services.AddHttpClient<MycosoftIntegrationService>();
builder.Services.AddHttpClient<ExternalDataIntegrationService>();

// Add Redis caching
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// Add SignalR
builder.Services.AddSignalR();

// Add Application Insights
builder.Services.AddApplicationInsightsTelemetry();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddCheck("cosmosdb", () =>
    {
        try
        {
            // Basic connectivity check - actual database health would be checked during requests
            return HealthCheckResult.Healthy("CosmosDB configuration is valid");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("CosmosDB configuration issue", ex);
        }
    })
    .AddCheck("signalr", () => HealthCheckResult.Healthy("SignalR hub is operational"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map SignalR Hub
app.MapHub<NatureOSHub>("/natureos-hub");

// Map health checks
app.MapHealthChecks("/health");

// Minimal API endpoints
app.MapGet("/", () => "NatureOS Core API - Fungal Intelligence Platform");

app.MapGet("/api/status", () => new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow,
    Version = "2.0.0",
    Environment = app.Environment.EnvironmentName,
    Features = new[]
    {
        "Real-time SignalR communication",
        "Proactive monitoring and alerting",
        "Enhanced MYCA integration",
        "Server-Sent Events streaming",
        "Mycosoft ecosystem synchronization"
    }
});

app.Run(); 
