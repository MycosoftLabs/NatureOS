using Microsoft.AspNetCore.Mvc;
using NatureOS.CoreApi.Services;

namespace NatureOS.CoreApi.Controllers;

/// <summary>
/// API controller for system health, metrics, and alerts
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MonitoringController : ControllerBase
{
    private readonly IMonitoringService _monitoringService;
    private readonly DeepAgentEventService _deepAgentEvents;
    private readonly ILogger<MonitoringController> _logger;

    public MonitoringController(
        IMonitoringService monitoringService,
        DeepAgentEventService deepAgentEvents,
        ILogger<MonitoringController> logger)
    {
        _monitoringService = monitoringService;
        _deepAgentEvents = deepAgentEvents;
        _logger = logger;
    }

    /// <summary>
    /// Get current system metrics (CPU, memory, disk)
    /// </summary>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(SystemMetrics), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<SystemMetrics>> GetMetrics(CancellationToken cancellationToken = default)
    {
        try
        {
            var metrics = await _monitoringService.GetSystemMetricsAsync(cancellationToken);
            await _deepAgentEvents.PublishAsync(
                domain: "natureos",
                task: "NatureOS monitoring metrics requested",
                context: new { route = "/api/Monitoring/metrics" },
                preferredAgent: "ops-agent",
                cancellationToken: cancellationToken);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get system metrics");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get recent system alerts
    /// </summary>
    [HttpGet("alerts")]
    [ProducesResponseType(typeof(IEnumerable<SystemAlert>), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<SystemAlert>>> GetAlerts(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var alerts = await _monitoringService.GetAlertsAsync(limit, cancellationToken);
            await _deepAgentEvents.PublishAsync(
                domain: "security",
                task: "NatureOS monitoring alerts requested",
                context: new { route = "/api/Monitoring/alerts", limit },
                preferredAgent: "security-agent",
                cancellationToken: cancellationToken);
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get alerts");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get overall system health status
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(SystemHealthStatus), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<SystemHealthStatus>> GetHealth(CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await _monitoringService.GetHealthStatusAsync(cancellationToken);
            await _deepAgentEvents.PublishAsync(
                domain: "natureos",
                task: "NatureOS health status requested",
                context: new { route = "/api/Monitoring/health" },
                preferredAgent: "ops-agent",
                cancellationToken: cancellationToken);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get health status");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get device health summary
    /// </summary>
    [HttpGet("devices")]
    [ProducesResponseType(typeof(DeviceHealthSummary), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<DeviceHealthSummary>> GetDeviceHealth(CancellationToken cancellationToken = default)
    {
        try
        {
            var summary = await _monitoringService.GetDeviceHealthAsync(cancellationToken);
            await _deepAgentEvents.PublishAsync(
                domain: "device",
                task: "NatureOS device health requested",
                context: new { route = "/api/Monitoring/devices" },
                preferredAgent: "ops-agent",
                cancellationToken: cancellationToken);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get device health");
            return StatusCode(500, "Internal server error");
        }
    }
}
