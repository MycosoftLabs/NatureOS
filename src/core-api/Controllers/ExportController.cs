using Microsoft.AspNetCore.Mvc;
using NatureOS.CoreApi.Services;

namespace NatureOS.CoreApi.Controllers;

/// <summary>
/// API controller for data export (CSV, JSON, FASTA)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExportController : ControllerBase
{
    private readonly IExportService _exportService;
    private readonly ILogger<ExportController> _logger;

    public ExportController(IExportService exportService, ILogger<ExportController> logger)
    {
        _exportService = exportService;
        _logger = logger;
    }

    /// <summary>
    /// Export data to CSV format
    /// </summary>
    [HttpPost("csv")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> ExportCsv(
        [FromBody] ExportRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = request.ToQuery();
            var result = await _exportService.ExportToCsvAsync(request.DataType ?? "events", query, cancellationToken);
            return File(
                System.Text.Encoding.UTF8.GetBytes(result.Content),
                result.ContentType,
                result.Filename ?? "export.csv");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export CSV");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Export data to JSON format
    /// </summary>
    [HttpPost("json")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> ExportJson(
        [FromBody] ExportRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = request.ToQuery();
            var result = await _exportService.ExportToJsonAsync(request.DataType ?? "events", query, cancellationToken);
            return File(
                System.Text.Encoding.UTF8.GetBytes(result.Content),
                result.ContentType,
                result.Filename ?? "export.json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export JSON");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Export sequences to FASTA format
    /// </summary>
    [HttpPost("fasta")]
    [ProducesResponseType(200)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> ExportFasta(
        [FromBody] ExportRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = request?.ToQuery() ?? new ExportQuery();
            var result = await _exportService.ExportToFastaAsync(query, cancellationToken);
            return File(
                System.Text.Encoding.UTF8.GetBytes(result.Content),
                result.ContentType,
                result.Filename ?? "sequences.fasta");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export FASTA");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get available export data types
    /// </summary>
    [HttpGet("types")]
    [ProducesResponseType(typeof(IEnumerable<string>), 200)]
    public ActionResult<IEnumerable<string>> GetDataTypes()
    {
        return Ok(_exportService.GetAvailableDataTypes());
    }
}

/// <summary>
/// Export request
/// </summary>
public class ExportRequest
{
    public string? DataType { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? DeviceId { get; set; }
    public string? Filter { get; set; }
    public int MaxRecords { get; set; } = 10000;

    public ExportQuery ToQuery() => new()
    {
        StartTime = StartTime,
        EndTime = EndTime,
        DeviceId = DeviceId,
        Filter = Filter,
        MaxRecords = MaxRecords
    };
}
