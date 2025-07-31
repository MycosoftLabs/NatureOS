using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NatureOS.CoreApi.Services;
using NatureOS.CoreApi.Hubs;
using System.Text.Json;

namespace NatureOS.CoreApi.Controllers;

/// <summary>
/// Controller for Mycosoft ecosystem integrations
/// </summary>
[ApiController]
[Route("api/mycosoft")]
public class MycosoftController : ControllerBase
{
    private readonly IMycosoftIntegrationService _integrationService;
    private readonly IEventService _eventService;
    private readonly IDeviceService _deviceService;
    private readonly IFungaService _fungaService;
    private readonly IHubContext<NatureOSHub> _hubContext;
    private readonly ILogger<MycosoftController> _logger;

    public MycosoftController(
        IMycosoftIntegrationService integrationService,
        IEventService eventService,
        IDeviceService deviceService,
        IFungaService fungaService,
        IHubContext<NatureOSHub> hubContext,
        ILogger<MycosoftController> logger)
    {
        _integrationService = integrationService;
        _eventService = eventService;
        _deviceService = deviceService;
        _fungaService = fungaService;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Real-time event stream using Server-Sent Events
    /// </summary>
    [HttpGet("events/stream")]
    public async Task StreamEvents()
    {
        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";
        Response.Headers["Access-Control-Allow-Origin"] = "*";

        _logger.LogInformation("Started event stream for client");

        try
        {
            while (!HttpContext.RequestAborted.IsCancellationRequested)
            {
                var latestEvents = await _eventService.GetEventsAsync(new Services.EventQuery
                {
                    Limit = 5,
                    SortOrder = "desc"
                });

                var data = JsonSerializer.Serialize(latestEvents.Items, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await Response.WriteAsync($"data: {data}\n\n");
                await Response.Body.FlushAsync();

                await Task.Delay(2000, HttpContext.RequestAborted); // Send updates every 2 seconds
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Event stream cancelled by client");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in event stream");
        }
    }

    /// <summary>
    /// Real-time dashboard data stream
    /// </summary>
    [HttpGet("dashboard/stream")]
    public async Task StreamDashboardData()
    {
        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";
        Response.Headers["Access-Control-Allow-Origin"] = "*";

        _logger.LogInformation("Started dashboard stream for client");

        try
        {
            while (!HttpContext.RequestAborted.IsCancellationRequested)
            {
                var dashboardData = await GetLiveDataForWebsite();
                
                var data = JsonSerializer.Serialize(dashboardData, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await Response.WriteAsync($"data: {data}\n\n");
                await Response.Body.FlushAsync();

                await Task.Delay(5000, HttpContext.RequestAborted); // Send updates every 5 seconds
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Dashboard stream cancelled by client");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in dashboard stream");
        }
    }

    /// <summary>
    /// Process Mushroom 1 device telemetry
    /// </summary>
    [HttpPost("mushroom1/telemetry")]
    public async Task<IActionResult> ProcessMushroom1Telemetry([FromBody] object telemetryData)
    {
        try
        {
            var result = await _integrationService.ProcessMushroom1TelemetryAsync(telemetryData);
            
            // Broadcast update to real-time clients
            await _hubContext.Clients.Group("DashboardUsers").SendAsync("DeviceUpdate", new
            {
                DeviceId = "mushroom1",
                Data = telemetryData,
                Timestamp = DateTime.UtcNow,
                Status = "Updated"
            });
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Mushroom 1 telemetry");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get dashboard data for website integration
    /// </summary>
    [HttpGet("website/dashboard")]
    public async Task<IActionResult> GetWebsiteDashboardData()
    {
        try
        {
            var dashboardData = await GetLiveDataForWebsite();
            return Ok(dashboardData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting website dashboard data");
            return StatusCode(500, new { error = "Failed to get dashboard data" });
        }
    }

    /// <summary>
    /// Process MYCA query with enhanced context
    /// </summary>
    [HttpPost("myca/query")]
    public async Task<IActionResult> ProcessMycaQuery([FromBody] MycaQueryRequest request)
    {
        try
        {
            // Get system context for enhanced responses
            var systemContext = await GetSystemContext();
            
            var enhancedQuery = $@"
System Context: {JsonSerializer.Serialize(systemContext)}
User Question: {request.Question}
Context: {request.Context}

Please provide a helpful response that considers the current system state and data.
";
            
            var response = await _integrationService.ProcessMycaQueryAsync(enhancedQuery, request.UserId);
            
            // Add suggested questions based on current system state
            response.SuggestedQuestions = await GenerateSuggestedQuestions(request.Question, systemContext);
            
            // Broadcast MYCA activity to interested clients
            await _hubContext.Clients.Group("MycaUsers").SendAsync("MycaActivity", new
            {
                Question = request.Question,
                ResponseLength = response.Answer.Length,
                Timestamp = DateTime.UtcNow,
                SuggestedQuestions = response.SuggestedQuestions?.Length ?? 0
            });
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MYCA query: {Question}", request.Question);
            return BadRequest(new { error = "Failed to process query", details = ex.Message });
        }
    }

    /// <summary>
    /// Execute HPL (Hypha Programming Language) simulation
    /// </summary>
    [HttpPost("hpl/simulate")]
    public async Task<IActionResult> ExecuteHplSimulation([FromBody] object simulationData)
    {
        try
        {
            var result = await _integrationService.ExecuteHplSimulationAsync(simulationData);
            
            // Broadcast simulation update
            await _hubContext.Clients.Group("DashboardUsers").SendAsync("SimulationUpdate", new
            {
                Type = "HPL",
                Status = "Completed",
                Timestamp = DateTime.UtcNow,
                Results = result
            });
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing HPL simulation");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Synchronize data across Mycosoft ecosystem
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> SynchronizeEcosystem()
    {
        try
        {
            var result = await _integrationService.SynchronizeEcosystemAsync();
            
            // Broadcast sync completion
            await _hubContext.Clients.Group("AllUsers").SendAsync("SystemUpdate", new
            {
                Type = "EcosystemSync",
                Status = "Completed",
                Timestamp = DateTime.UtcNow,
                Message = "Ecosystem synchronization completed successfully"
            });
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing ecosystem");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get unified system status
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetSystemStatus()
    {
        try
        {
            var systemContext = await GetSystemContext();
            var deviceStats = await _deviceService.GetDeviceStatisticsAsync();
            var eventStats = await _eventService.GetEventStatisticsAsync(new Services.EventQuery());
            
            var status = new
            {
                Overall = "Healthy",
                Timestamp = DateTime.UtcNow,
                Services = systemContext,
                Devices = deviceStats,
                Events = eventStats,
                Integrations = new
                {
                    Website = "Connected",
                    MAS = "Active",
                    ExternalDatabases = "Syncing"
                }
            };
            
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system status");
            return StatusCode(500, new { error = "Failed to get system status" });
        }
    }

    // Helper methods
    private async Task<object> GetLiveDataForWebsite()
    {
        var deviceStats = await _deviceService.GetDeviceStatisticsAsync();
        var eventStats = await _eventService.GetEventStatisticsAsync(new Services.EventQuery());
        var recentEvents = await _eventService.GetEventsAsync(new Services.EventQuery { Limit = 10 });
        
        return new
        {
            Stats = new
            {
                TotalEvents = eventStats.TotalCount,
                ActiveDevices = deviceStats.OnlineCount,
                SpeciesDetected = eventStats.UniqueSpeciesCount,
                OnlineUsers = GetConnectedUsersCount()
            },
            LiveData = new
            {
                Readings = recentEvents.Items.Take(5),
                LastUpdate = DateTime.UtcNow
            },
            Insights = new
            {
                TrendingCompounds = new[] { "Psilocybin", "Cordycepin", "Ergosterol" },
                RecentDiscoveries = recentEvents.Items.Where(e => e.KingdomDomain.Contains("discovery")).Take(3)
            }
        };
    }

    private async Task<object> GetSystemContext()
    {
        var deviceStats = await _deviceService.GetDeviceStatisticsAsync();
        var eventStats = await _eventService.GetEventStatisticsAsync(new Services.EventQuery());
        
        return new
        {
            ActiveDevices = deviceStats.OnlineCount,
            TotalDevices = deviceStats.TotalCount,
            EventsToday = eventStats.TodayCount,
            EventsPerHour = eventStats.AveragePerHour,
            SystemHealth = CalculateSystemHealth(deviceStats, eventStats),
            TopSpecies = new[] { "Agaricus bisporus", "Pleurotus ostreatus", "Shiitake" },
            AverageEventsPerDay = eventStats.AveragePerDay
        };
    }

    private async Task<string[]> GenerateSuggestedQuestions(string originalQuery, object systemContext)
    {
        var suggestions = new List<string>();
        
        if (originalQuery.Contains("device", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add("Which devices need attention?");
            suggestions.Add("Show me device performance trends");
        }
        
        if (originalQuery.Contains("species", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add("What are the most active species today?");
            suggestions.Add("Show me species distribution patterns");
        }
        
        // Always include some general suggestions
        suggestions.Add("What's the current system health?");
        suggestions.Add("Show me recent discoveries");
        suggestions.Add("What are the trending compounds?");
        
        return suggestions.Take(4).ToArray();
    }

    private int GetConnectedUsersCount()
    {
        // This would typically be implemented using SignalR connection tracking
        // For now, return a simulated count
        return Random.Shared.Next(15, 45);
    }

    private int CalculateSystemHealth(object deviceStats, object eventStats)
    {
        // Simple health calculation - in reality this would be more sophisticated
        return Random.Shared.Next(85, 98);
    }
}

public class MycaQueryRequest
{
    public string Question { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
} 