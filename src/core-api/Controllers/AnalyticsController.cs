using Microsoft.AspNetCore.Mvc;
using NatureOS.CoreApi.Services;

namespace NatureOS.CoreApi.Controllers;

/// <summary>
/// API controller for data analytics, reports, and insights
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(IAnalyticsService analyticsService, ILogger<AnalyticsController> logger)
    {
        _analyticsService = analyticsService;
        _logger = logger;
    }

    /// <summary>
    /// Get time-series data for a metric
    /// </summary>
    [HttpGet("timeseries")]
    [ProducesResponseType(typeof(TimeSeriesResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<TimeSeriesResult>> GetTimeSeries(
        [FromQuery] string metric = "event_count",
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] string? deviceId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var startTime = start ?? DateTime.UtcNow.AddDays(-7);
            var endTime = end ?? DateTime.UtcNow;
            if (startTime >= endTime)
                return BadRequest("Start time must be before end time");

            var result = await _analyticsService.GetTimeSeriesAsync(metric, startTime, endTime, deviceId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get time-series data");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get biodiversity metrics
    /// </summary>
    [HttpGet("biodiversity")]
    [ProducesResponseType(typeof(AnalyticsBiodiversityMetrics), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<AnalyticsBiodiversityMetrics>> GetBiodiversity(
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metrics = await _analyticsService.GetBiodiversityMetricsAsync(start, end, cancellationToken);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get biodiversity metrics");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get analytics report
    /// </summary>
    [HttpGet("reports/{reportType}")]
    [ProducesResponseType(typeof(AnalyticsReport), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<AnalyticsReport>> GetReport(
        string reportType,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var report = await _analyticsService.GetReportAsync(reportType, start, end, cancellationToken);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get report {ReportType}", reportType);
            return StatusCode(500, "Internal server error");
        }
    }
}
