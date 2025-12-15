using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NatureOS.CoreApi.Services;
using NatureOS.CoreApi.Hubs;
using NatureOS.MINDEX.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    private readonly IMasIngestionService _masIngestionService;
    private readonly IFeedbackStore _feedback;
    private readonly IChatContextService _chatCtx;
    private readonly ILogger<MycosoftController> _logger;

    public MycosoftController(
        IMycosoftIntegrationService integrationService,
        IEventService eventService,
        IDeviceService deviceService,
        IFungaService fungaService,
        IHubContext<NatureOSHub> hubContext,
        IMasIngestionService masIngestionService,
        IFeedbackStore feedback,
        IChatContextService chatCtx,
        ILogger<MycosoftController> logger)
    {
        _integrationService = integrationService;
        _eventService = eventService;
        _deviceService = deviceService;
        _fungaService = fungaService;
        _hubContext = hubContext;
        _masIngestionService = masIngestionService;
        _feedback = feedback;
        _chatCtx = chatCtx;
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

                await Task.Delay(2000, HttpContext.RequestAborted);
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
    /// Ingest website or service context into MAS knowledge graph
    /// </summary>
    [HttpPost("mas/ingest")]
    public async Task<IActionResult> IngestMasContext([FromBody] object payload)
    {
        try
        {
            var ok = await _masIngestionService.IngestContextAsync(payload);
            if (!ok) return StatusCode(500, new { error = "Failed to ingest context" });

            await _hubContext.Clients.Group("MycaUsers").SendAsync("SystemUpdate", new
            {
                Type = "MASContextIngested",
                Timestamp = DateTime.UtcNow
            });

            return Ok(new { status = "ok" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting MAS context");
            return StatusCode(500, new { error = ex.Message });
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
                var dashboardData = await BuildWebsiteDashboardAsync(HttpContext.RequestAborted);

                var data = JsonSerializer.Serialize(dashboardData, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await Response.WriteAsync($"data: {data}\n\n");
                await Response.Body.FlushAsync();

                await Task.Delay(5000, HttpContext.RequestAborted);
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
    /// EXISTING: Get dashboard data for website integration
    /// Canonical response: { Stats, LiveData, Insights }
    /// </summary>
    [HttpGet("website/dashboard")]
    public async Task<IActionResult> GetWebsiteDashboardData(CancellationToken cancellationToken)
    {
        try
        {
            var dashboardData = await BuildWebsiteDashboardAsync(cancellationToken);
            return Ok(dashboardData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting website dashboard data");
            return StatusCode(500, new { error = "Failed to get dashboard data" });
        }
    }

    /// <summary>
    /// NEW: mirror of what the website expects for suggestions/context
    /// Canonical response: { readings[], context }
    /// </summary>
    [HttpGet("website/live-data")]
    public async Task<IActionResult> GetWebsiteLiveData(CancellationToken cancellationToken)
    {
        try
        {
            var recent = await _eventService.GetEventsAsync(new Services.EventQuery
            {
                Limit = 20,
                SortOrder = "desc"
            }, cancellationToken);

            var ctx = await _chatCtx.BuildLightweightContextAsync(cancellationToken);

            var readings = recent.Items.Select(r => new WebsiteLiveReading
            {
                DeviceId = r.SourceDevice,
                Value = ExtractPrimaryValue(r),
                Ts = r.Timestamp
            }).ToArray();

            return Ok(new WebsiteLiveDataResponse
            {
                Readings = readings,
                Context = ctx
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting website live data");
            return StatusCode(500, new { error = "Failed to get live data" });
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
            var systemContext = await GetSystemContext();

            var enhancedQuery = $@"
System Context: {JsonSerializer.Serialize(systemContext)}
User Question: {request.Question}
Context: {request.Context}

Please provide a helpful response that considers the current system state and data.
";

            var response = await _integrationService.ProcessMycaQueryAsync(enhancedQuery, request.UserId);
            response.SuggestedQuestions = await GenerateSuggestedQuestions(request.Question, systemContext);

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
    /// NEW: feedback sink used by website/MAS
    /// Accepts { conversationId, feedback, note? }
    /// </summary>
    public sealed class FeedbackRequest
    {
        [JsonPropertyName("conversationId")]
        public required string ConversationId { get; init; }

        [JsonPropertyName("feedback")]
        public required string Feedback { get; init; }

        [JsonPropertyName("note")]
        public string? Note { get; init; }
    }

    [HttpPost("myca/feedback")]
    public async Task<IActionResult> RecordMycaFeedback([FromBody] FeedbackRequest req, CancellationToken cancellationToken)
    {
        await _feedback.AppendAsync(new FeedbackEntry
        {
            ConversationId = req.ConversationId,
            Feedback = req.Feedback,
            Note = req.Note,
            TimestampUtc = DateTime.UtcNow
        }, cancellationToken);

        return Accepted(new { status = "ok" });
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

    // --- Helpers ---

    private async Task<object> GetSystemContext()
    {
        var deviceStats = await _deviceService.GetDeviceStatisticsAsync();
        var eventStats = await _eventService.GetEventStatisticsAsync(new Services.EventQuery());

        return new
        {
            ActiveDevices = deviceStats.ActiveDevices,
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

        suggestions.Add("What's the current system health?");
        suggestions.Add("Show me recent discoveries");
        suggestions.Add("What are the trending compounds?");

        return suggestions.Take(4).ToArray();
    }

    private int GetConnectedUsersCount()
    {
        return Random.Shared.Next(15, 45);
    }

    private int CalculateSystemHealth(object deviceStats, object eventStats)
    {
        return Random.Shared.Next(85, 98);
    }

    // Canonical website/dashboard response object
    private async Task<WebsiteDashboardResponse> BuildWebsiteDashboardAsync(CancellationToken cancellationToken)
    {
        var eventStats = await _eventService.GetEventStatisticsAsync(new Services.EventQuery(), cancellationToken);
        var deviceStats = await _deviceService.GetDeviceStatisticsAsync(cancellationToken);
        var recentEvents = await _eventService.GetEventsAsync(new Services.EventQuery { Limit = 50, SortOrder = "desc" }, cancellationToken);

        deviceStats.DevicesByStatus.TryGetValue(DeviceStatus.Online, out var onlineCount);

        var last24h = await _eventService.GetEventsByTimeRangeAsync(DateTime.UtcNow.AddHours(-24), DateTime.UtcNow, limit: 1000, cancellationToken);
        var errorsLast24h = last24h.Count(e => e.KingdomDomain.Contains("error", StringComparison.OrdinalIgnoreCase));

        var readings = recentEvents.Items.Select(e => new WebsiteDashboardReading
        {
            DeviceId = e.SourceDevice,
            Value = ExtractPrimaryValue(e),
            Timestamp = e.Timestamp
        }).ToArray();

        return new WebsiteDashboardResponse
        {
            Stats = new WebsiteDashboardStats
            {
                TotalEvents = eventStats.TotalCount,
                ActiveDevices = onlineCount,
                ErrorsLast24h = errorsLast24h
            },
            LiveData = new WebsiteDashboardLiveData
            {
                Readings = readings,
                LastUpdate = DateTime.UtcNow
            },
            Insights = new WebsiteDashboardInsights
            {
                TrendingCompounds = new[] { "Psilocybin", "Cordycepin", "Ergosterol" },
                RecentDiscoveries = recentEvents.Items.Where(e => e.KingdomDomain.Contains("discovery", StringComparison.OrdinalIgnoreCase)).Take(3).Cast<object>().ToArray()
            }
        };
    }

    private static object? ExtractPrimaryValue(MycorrhizaeEvent ev)
    {
        try
        {
            if (ev.SignalVector is JsonElement je)
            {
                if (je.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number) return v.GetDouble();
                if (je.TryGetProperty("temperature", out var t) && t.ValueKind == JsonValueKind.Number) return t.GetDouble();
                if (je.TryGetProperty("humidity", out var h) && h.ValueKind == JsonValueKind.Number) return h.GetDouble();
                if (je.TryGetProperty("side_a", out var sideA) && sideA.ValueKind == JsonValueKind.Object)
                {
                    if (sideA.TryGetProperty("bme688", out var bme) && bme.ValueKind == JsonValueKind.Object)
                    {
                        if (bme.TryGetProperty("temperature", out var bt) && bt.ValueKind == JsonValueKind.Number) return bt.GetDouble();
                        if (bme.TryGetProperty("humidity", out var bh) && bh.ValueKind == JsonValueKind.Number) return bh.GetDouble();
                    }
                }
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    public sealed class WebsiteDashboardResponse
    {
        // Preserve canonical top-level keys (do not camelCase these)
        [JsonPropertyName("Stats")]
        public required WebsiteDashboardStats Stats { get; init; }

        [JsonPropertyName("LiveData")]
        public required WebsiteDashboardLiveData LiveData { get; init; }

        [JsonPropertyName("Insights")]
        public required WebsiteDashboardInsights Insights { get; init; }
    }

    public sealed class WebsiteDashboardStats
    {
        public long TotalEvents { get; init; }
        public long ActiveDevices { get; init; }
        public long ErrorsLast24h { get; init; }
    }

    public sealed class WebsiteDashboardLiveData
    {
        public required WebsiteDashboardReading[] Readings { get; init; }
        public DateTime LastUpdate { get; init; }
    }

    public sealed class WebsiteDashboardReading
    {
        public required string DeviceId { get; init; }
        public object? Value { get; init; }
        public DateTime Timestamp { get; init; }
    }

    public sealed class WebsiteDashboardInsights
    {
        public required string[] TrendingCompounds { get; init; }
        public required object[] RecentDiscoveries { get; init; }
    }

    public sealed class WebsiteLiveDataResponse
    {
        [JsonPropertyName("readings")]
        public required WebsiteLiveReading[] Readings { get; init; }

        [JsonPropertyName("context")]
        public required object Context { get; init; }
    }

    public sealed class WebsiteLiveReading
    {
        [JsonPropertyName("deviceId")]
        public required string DeviceId { get; init; }

        [JsonPropertyName("value")]
        public object? Value { get; init; }

        [JsonPropertyName("ts")]
        public DateTime Ts { get; init; }
    }
}

public class MycaQueryRequest
{
    public string Question { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
}
