using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Cosmos;
using Microsoft.OpenApi.Models;
using NatureOS.CoreApi.Services;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger/OpenAPI configuration
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "NatureOS Core API",
        Version = "v1",
        Description = "Cloud-native operating system for nature - Core API",
        Contact = new OpenApiContact
        {
            Name = "Mycosoft Labs",
            Email = "api@mycosoft.com",
            Url = new Uri("https://github.com/MycosoftLabs/NatureOS")
        }
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("NatureOSPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["AzureAd:Authority"];
        options.Audience = builder.Configuration["AzureAd:Audience"];
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidateLifetime = true;
    });

// Azure services
builder.Services.AddSingleton(provider =>
{
    var connectionString = builder.Configuration.GetConnectionString("CosmosDb");
    return new CosmosClient(connectionString);
});

// Application services
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IFungaService, FungaService>();
builder.Services.AddScoped<IMycosoftIntegrationService, MycosoftIntegrationService>();

// HTTP client for external services
builder.Services.AddHttpClient<FungaService>();
builder.Services.AddHttpClient<MycosoftIntegrationService>();

// Health checks
builder.Services.AddHealthChecks()
    .AddCosmosDb(builder.Configuration.GetConnectionString("CosmosDb") ?? "");

// Application Insights
builder.Services.AddApplicationInsightsTelemetry();

// Logging
builder.Logging.AddApplicationInsights();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "NatureOS Core API v1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at root
    });
}

app.UseHttpsRedirection();
app.UseCors("NatureOSPolicy");
app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint
app.MapHealthChecks("/health");

// API routes
app.MapControllers();

// Minimal API endpoints for quick access
app.MapGet("/", () => new
{
    Name = "NatureOS Core API",
    Version = "1.0.0",
    Description = "Cloud-native operating system for nature",
    Endpoints = new
    {
        Events = "/api/events",
        Devices = "/api/devices",
        Funga = "/api/funga",
        Health = "/health",
        Swagger = "/swagger"
    }
});

app.Run(); 