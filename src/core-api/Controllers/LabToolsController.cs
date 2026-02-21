using Microsoft.AspNetCore.Mvc;
using NatureOS.CoreApi.Services;

namespace NatureOS.CoreApi.Controllers;

/// <summary>
/// API controller for lab-specific operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class LabToolsController : ControllerBase
{
    private readonly ILabToolsService _labToolsService;
    private readonly ILogger<LabToolsController> _logger;

    public LabToolsController(ILabToolsService labToolsService, ILogger<LabToolsController> logger)
    {
        _labToolsService = labToolsService;
        _logger = logger;
    }

    /// <summary>
    /// Get list of lab samples
    /// </summary>
    [HttpGet("samples")]
    [ProducesResponseType(typeof(IEnumerable<LabSample>), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<LabSample>>> GetSamples(
        [FromQuery] string? filter = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var samples = await _labToolsService.GetSamplesAsync(filter, limit, cancellationToken);
            return Ok(samples);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get samples");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get a sample by ID
    /// </summary>
    [HttpGet("samples/{sampleId}")]
    [ProducesResponseType(typeof(LabSample), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<LabSample>> GetSample(
        string sampleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sample = await _labToolsService.GetSampleAsync(sampleId, cancellationToken);
            if (sample == null)
                return NotFound($"Sample {sampleId} not found");
            return Ok(sample);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get sample {SampleId}", sampleId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Register a new sample
    /// </summary>
    [HttpPost("samples")]
    [ProducesResponseType(typeof(LabSample), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<LabSample>> RegisterSample(
        [FromBody] LabSample sample,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(sample.Name))
                return BadRequest("Sample name is required");

            var created = await _labToolsService.RegisterSampleAsync(sample, cancellationToken);
            return CreatedAtAction(nameof(GetSample), new { sampleId = created.SampleId }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register sample");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get available protocols
    /// </summary>
    [HttpGet("protocols")]
    [ProducesResponseType(typeof(IEnumerable<LabProtocol>), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<LabProtocol>>> GetProtocols(CancellationToken cancellationToken = default)
    {
        try
        {
            var protocols = await _labToolsService.GetProtocolsAsync(cancellationToken);
            return Ok(protocols);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get protocols");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get protocol by ID
    /// </summary>
    [HttpGet("protocols/{protocolId}")]
    [ProducesResponseType(typeof(LabProtocol), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<LabProtocol>> GetProtocol(
        string protocolId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var protocol = await _labToolsService.GetProtocolAsync(protocolId, cancellationToken);
            if (protocol == null)
                return NotFound($"Protocol {protocolId} not found");
            return Ok(protocol);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get protocol {ProtocolId}", protocolId);
            return StatusCode(500, "Internal server error");
        }
    }
}
