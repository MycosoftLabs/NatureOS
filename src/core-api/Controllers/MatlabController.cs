using Microsoft.AspNetCore.Mvc;
using NatureOS.CoreApi.Services;

namespace NatureOS.CoreApi.Controllers;

/// <summary>
/// API controller for MATLAB-driven analytics, AI, and visualization
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MatlabController : ControllerBase
{
    private readonly IMatlabIntegrationService _matlabService;
    private readonly ILogger<MatlabController> _logger;

    public MatlabController(IMatlabIntegrationService matlabService, ILogger<MatlabController> logger)
    {
        _matlabService = matlabService;
        _logger = logger;
    }

    /// <summary>
    /// Execute a named MATLAB analysis function
    /// </summary>
    [HttpPost("analysis/{functionName}")]
    [ProducesResponseType(typeof(MatlabResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<MatlabResult>> ExecuteAnalysis(
        string functionName,
        [FromBody] object[] args,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(functionName))
                return BadRequest("Function name is required");
            var result = await _matlabService.ExecuteAnalysisAsync(functionName, args ?? Array.Empty<object>(), cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute MATLAB analysis {Function}", functionName);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Run anomaly detection on telemetry time-series data
    /// </summary>
    [HttpPost("anomaly-detection")]
    [ProducesResponseType(typeof(AnomalyDetectionResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<AnomalyDetectionResult>> AnomalyDetection(
        [FromBody] double[] timeSeries,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (timeSeries == null || timeSeries.Length == 0)
                return BadRequest("Time series data is required");
            var result = await _matlabService.DetectAnomaliesAsync(timeSeries, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run anomaly detection");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Environmental forecasting for a metric
    /// </summary>
    [HttpPost("forecast")]
    [ProducesResponseType(typeof(ForecastResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ForecastResult>> Forecast(
        [FromBody] ForecastRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Metric))
                return BadRequest("Metric and horizon are required");
            var result = await _matlabService.ForecastEnvironmentalAsync(
                request.Metric,
                request.HorizonHours,
                request.HistoricalData,
                cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run forecast");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Generate a visualization (plot) and return as image
    /// </summary>
    [HttpPost("visualization")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Visualization(
        [FromBody] VisualizationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.PlotType))
                return BadRequest("Plot type and data are required");
            var bytes = await _matlabService.GenerateVisualizationAsync(
                request.PlotType,
                request.Data ?? new object(),
                cancellationToken);
            return File(bytes, "image/png", "plot.png");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate visualization");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Classify fungal morphology from signal vector
    /// </summary>
    [HttpPost("classification")]
    [ProducesResponseType(typeof(ClassificationResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ClassificationResult>> Classification(
        [FromBody] double[] signalVector,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (signalVector == null || signalVector.Length == 0)
                return BadRequest("Signal vector is required");
            var result = await _matlabService.ClassifyFungalMorphologyAsync(signalVector, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run classification");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get MATLAB engine/Production Server health status
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(MatlabHealthStatus), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<MatlabHealthStatus>> Health(CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await _matlabService.GetHealthStatusAsync(cancellationToken);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get MATLAB health");
            return StatusCode(500, "Internal server error");
        }
    }
}

/// <summary>
/// Request for forecast endpoint
/// </summary>
public class ForecastRequest
{
    public string Metric { get; set; } = string.Empty;
    public int HorizonHours { get; set; }
    public double[]? HistoricalData { get; set; }
}

/// <summary>
/// Request for visualization endpoint
/// </summary>
public class VisualizationRequest
{
    public string PlotType { get; set; } = string.Empty;
    public object? Data { get; set; }
}
