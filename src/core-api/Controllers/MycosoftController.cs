using Microsoft.AspNetCore.Mvc;
using NatureOS.CoreApi.Services;
using NatureOS.MINDEX.Models;

namespace NatureOS.CoreApi.Controllers;

/// <summary>
/// API controller for Mycosoft product integrations
/// </summary>
[ApiController]
[Route("api/mycosoft")]
[Produces("application/json")]
public class MycosoftController : ControllerBase
{
    private readonly IMycosoftIntegrationService _integrationService;
    private readonly ILogger<MycosoftController> _logger;

    public MycosoftController(
        IMycosoftIntegrationService integrationService,
        ILogger<MycosoftController> logger)
    {
        _integrationService = integrationService;
        _logger = logger;
    }

    /// <summary>
    /// Endpoint for Mushroom 1 device telemetry
    /// </summary>
    [HttpPost("mushroom1/telemetry")]
    [ProducesResponseType(typeof(ProcessingResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ProcessingResult>> ProcessMushroom1Telemetry(
        [FromBody] Mushroom1Telemetry telemetry,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _integrationService.ProcessMushroom1DataAsync(telemetry, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Mushroom 1 telemetry");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get dashboard data for Mycosoft.com website
    /// </summary>
    [HttpGet("website/dashboard")]
    [ProducesResponseType(typeof(WebsiteDashboardData), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<WebsiteDashboardData>> GetWebsiteDashboard(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var syncRequest = new WebsiteSyncRequest
            {
                DataTypes = new[] { "events", "devices", "species", "users", "readings" },
                LastSync = DateTime.UtcNow.AddMinutes(-5)
            };

            await _integrationService.SyncWithWebsiteAsync(syncRequest, cancellationToken);
            
            // Return the latest dashboard data
            var dashboardData = new WebsiteDashboardData
            {
                TotalEvents = await GetTotalEventsAsync(),
                ActiveDevices = await GetActiveDevicesAsync(),
                SpeciesDetected = await GetSpeciesCountAsync(),
                OnlineUsers = GetOnlineUsersCount(),
                LiveReadings = await GetLiveReadingsAsync(),
                TrendingCompounds = GetTrendingCompounds(),
                RecentDiscoveries = GetRecentDiscoveries()
            };

            return Ok(dashboardData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get website dashboard data");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Execute HPL (Hypha Programming Language) simulation
    /// </summary>
    [HttpPost("hpl/simulate")]
    [ProducesResponseType(typeof(HplSimulationResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<HplSimulationResult>> ExecuteHplSimulation(
        [FromBody] HplSimulationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(request.HplCode))
            {
                return BadRequest("HPL code is required");
            }

            var result = await _integrationService.ProcessHplSimulationAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute HPL simulation");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get live data for website components
    /// </summary>
    [HttpGet("website/live-data")]
    [ProducesResponseType(typeof(LiveDataResponse), 200)]
    public async Task<ActionResult<LiveDataResponse>> GetLiveData(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var liveData = new LiveDataResponse
            {
                Timestamp = DateTime.UtcNow,
                EnvironmentalReadings = await GetLatestEnvironmentalDataAsync(limit),
                NetworkActivity = await GetNetworkActivityAsync(),
                SpeciesObservations = await GetRecentSpeciesObservationsAsync(limit),
                CompoundAnalysis = await GetCompoundAnalysisAsync(),
                SystemHealth = await GetSystemHealthAsync()
            };

            return Ok(liveData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get live data");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// MYCA AI Assistant endpoint
    /// </summary>
    [HttpPost("myca/query")]
    [ProducesResponseType(typeof(MycaResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<MycaResponse>> QueryMyca(
        [FromBody] MycaQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(query.Question))
            {
                return BadRequest("Question is required");
            }

            // This would integrate with the actual MYCA system
            var response = new MycaResponse
            {
                Answer = GenerateMycaResponse(query.Question),
                Confidence = 0.95,
                Sources = new[] { "MINDEX", "Fungi LLM", "Arboretum Dataset" },
                Timestamp = DateTime.UtcNow,
                SuggestedQuestions = new[]
                {
                    "What species are most active today?",
                    "Show me mycorrhizal network patterns",
                    "What compounds are trending?"
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query MYCA");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Trigger data sync with all Mycosoft services
    /// </summary>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(SyncResult), 200)]
    public async Task<ActionResult<SyncResult>> TriggerSync(
        [FromBody] SyncRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var syncTasks = new List<Task>();

            if (request.SyncTargets.Contains("website"))
            {
                syncTasks.Add(_integrationService.SyncWithWebsiteAsync(
                    new WebsiteSyncRequest { DataTypes = request.DataTypes }, cancellationToken));
            }

            if (request.SyncTargets.Contains("myca"))
            {
                // Sync with MYCA would be implemented here
                syncTasks.Add(Task.CompletedTask);
            }

            if (request.SyncTargets.Contains("mwave"))
            {
                // Sync with MWave would be implemented here
                syncTasks.Add(Task.CompletedTask);
            }

            await Task.WhenAll(syncTasks);

            return Ok(new SyncResult
            {
                Success = true,
                SyncedAt = DateTime.UtcNow,
                SyncedServices = request.SyncTargets.ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger sync");
            return StatusCode(500, "Internal server error");
        }
    }

    // Helper methods
    private async Task<long> GetTotalEventsAsync()
    {
        // Implementation would query MINDEX
        return 150000; // Placeholder
    }

    private async Task<int> GetActiveDevicesAsync()
    {
        // Implementation would query device registry
        return 42; // Placeholder
    }

    private async Task<int> GetSpeciesCountAsync()
    {
        // Implementation would query species database
        return 156; // Placeholder
    }

    private static int GetOnlineUsersCount()
    {
        return 23; // Placeholder
    }

    private async Task<List<LiveReading>> GetLiveReadingsAsync()
    {
        // Implementation would query latest telemetry
        return new List<LiveReading>
        {
            new() { DeviceId = "mushroom-001", Timestamp = DateTime.UtcNow, Value = 23.5 },
            new() { DeviceId = "mushroom-002", Timestamp = DateTime.UtcNow, Value = 22.8 },
            new() { DeviceId = "spore-det-001", Timestamp = DateTime.UtcNow, Value = 0.75 }
        };
    }

    private static List<string> GetTrendingCompounds()
    {
        return new List<string> { "Psilocybin", "Cordycepin", "Beta-glucan", "Ergothioneine" };
    }

    private static List<string> GetRecentDiscoveries()
    {
        return new List<string>
        {
            "New mycorrhizal network topology discovered",
            "Novel antifungal compound isolated",
            "Rare species found in urban environment"
        };
    }

    private async Task<List<EnvironmentalReading>> GetLatestEnvironmentalDataAsync(int limit)
    {
        return new List<EnvironmentalReading>
        {
            new() { Parameter = "Temperature", Value = 22.5, Unit = "°C", Timestamp = DateTime.UtcNow },
            new() { Parameter = "Humidity", Value = 78.2, Unit = "%", Timestamp = DateTime.UtcNow },
            new() { Parameter = "pH", Value = 6.8, Unit = "", Timestamp = DateTime.UtcNow }
        };
    }

    private async Task<NetworkActivityData> GetNetworkActivityAsync()
    {
        return new NetworkActivityData
        {
            ActiveConnections = 156,
            DataThroughput = 2.4, // MB/s
            PacketsPerSecond = 1250,
            NetworkHealth = "Optimal"
        };
    }

    private async Task<List<SpeciesObservation>> GetRecentSpeciesObservationsAsync(int limit)
    {
        return new List<SpeciesObservation>
        {
            new() { Species = "Amanita muscaria", Location = "Forest Site A", Timestamp = DateTime.UtcNow },
            new() { Species = "Pleurotus ostreatus", Location = "Lab Culture 23", Timestamp = DateTime.UtcNow }
        };
    }

    private async Task<CompoundAnalysisData> GetCompoundAnalysisAsync()
    {
        return new CompoundAnalysisData
        {
            TotalCompounds = 1247,
            NewCompoundsToday = 3,
            TrendingCompounds = GetTrendingCompounds()
        };
    }

    private async Task<SystemHealthData> GetSystemHealthAsync()
    {
        return new SystemHealthData
        {
            OverallHealth = "Healthy",
            ApiResponseTime = 45, // ms
            DatabaseConnections = 12,
            CpuUsage = 23.5,
            MemoryUsage = 67.8
        };
    }

    private static string GenerateMycaResponse(string question)
    {
        // This would integrate with the actual MYCA AI system
        var responses = new Dictionary<string, string>
        {
            ["species"] = "Based on current sensor data, I'm detecting 3 active species in your network: Amanita muscaria, Pleurotus ostreatus, and an unidentified Basidiomycete. The mycorrhizal connections show increased activity near sensor cluster B.",
            ["network"] = "The mycorrhizal network is showing strong interconnectivity with a clustering coefficient of 0.73. I observe 5 main hub nodes with extensive connections to surrounding mycelium.",
            ["compounds"] = "Today's trending bioactive compounds include Psilocybin (up 15%), Cordycepin (up 8%), and a newly detected terpene profile suggesting possible antimicrobial properties."
        };

        var lowerQuestion = question.ToLower();
        foreach (var (key, response) in responses)
        {
            if (lowerQuestion.Contains(key))
                return response;
        }

        return "I'm analyzing your data through the MINDEX database. Could you be more specific about what aspect of the fungal network you'd like to explore?";
    }
}

// Supporting models
public class LiveDataResponse
{
    public DateTime Timestamp { get; set; }
    public List<EnvironmentalReading> EnvironmentalReadings { get; set; } = new();
    public NetworkActivityData? NetworkActivity { get; set; }
    public List<SpeciesObservation> SpeciesObservations { get; set; } = new();
    public CompoundAnalysisData? CompoundAnalysis { get; set; }
    public SystemHealthData? SystemHealth { get; set; }
}

public class EnvironmentalReading
{
    public string Parameter { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class NetworkActivityData
{
    public int ActiveConnections { get; set; }
    public double DataThroughput { get; set; }
    public int PacketsPerSecond { get; set; }
    public string NetworkHealth { get; set; } = string.Empty;
}

public class SpeciesObservation
{
    public string Species { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class CompoundAnalysisData
{
    public int TotalCompounds { get; set; }
    public int NewCompoundsToday { get; set; }
    public List<string> TrendingCompounds { get; set; } = new();
}

public class SystemHealthData
{
    public string OverallHealth { get; set; } = string.Empty;
    public int ApiResponseTime { get; set; }
    public int DatabaseConnections { get; set; }
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
}

public class MycaQuery
{
    public string Question { get; set; } = string.Empty;
    public string? Context { get; set; }
    public string? UserId { get; set; }
}

public class MycaResponse
{
    public string Answer { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string[] Sources { get; set; } = Array.Empty<string>();
    public DateTime Timestamp { get; set; }
    public string[] SuggestedQuestions { get; set; } = Array.Empty<string>();
}

public class SyncRequest
{
    public List<string> SyncTargets { get; set; } = new();
    public string[] DataTypes { get; set; } = Array.Empty<string>();
}

public class SyncResult
{
    public bool Success { get; set; }
    public DateTime SyncedAt { get; set; }
    public List<string> SyncedServices { get; set; } = new();
    public string? ErrorMessage { get; set; }
} 