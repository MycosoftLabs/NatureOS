using Microsoft.AspNetCore.Mvc;
using NatureOS.CoreApi.Services;
using NatureOS.MINDEX.Models;

namespace NatureOS.CoreApi.Controllers;

[ApiController]
[Route("api/mycobrain")]
public class MycoBrainController : ControllerBase
{
    private readonly IMycoBrainService _service;
    private readonly ILogger<MycoBrainController> _logger;

    public MycoBrainController(IMycoBrainService service, ILogger<MycoBrainController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpPost("telemetry")]
    public async Task<IActionResult> Telemetry([FromBody] MycoBrainTelemetry telemetry, CancellationToken ct)
    {
        var result = await _service.ProcessTelemetryAsync(telemetry, ct);
        return Ok(result);
    }

    [HttpPost("telemetry/ndjson")]
    public async Task<IActionResult> TelemetryNdjson([FromBody] string jsonLine, CancellationToken ct)
    {
        var result = await _service.ProcessNDJSONLineAsync(jsonLine, ct);
        return Ok(result);
    }

    [HttpPost("telemetry/mdp")]
    public async Task<IActionResult> TelemetryMdp([FromBody] byte[] frame, CancellationToken ct)
    {
        var result = await _service.ProcessMDPFrameAsync(frame, ct);
        return Ok(result);
    }

    [HttpPost("command")]
    public async Task<IActionResult> SendCommand([FromBody] MycoBrainCommand command, CancellationToken ct)
    {
        var ok = await _service.SendCommandAsync(command, ct);
        return ok ? Ok(new { status = "sent" }) : StatusCode(500, new { error = "send_failed" });
    }

    [HttpPost("devices/register")]
    public async Task<IActionResult> Register([FromBody] MycoBrainDevice device, CancellationToken ct)
    {
        var saved = await _service.RegisterDeviceAsync(device, ct);
        return Ok(saved);
    }

    [HttpGet("devices")]
    public async Task<IActionResult> GetDevices(CancellationToken ct)
    {
        return Ok(await _service.GetDevicesAsync(ct));
    }

    [HttpGet("devices/{serial}")]
    public async Task<IActionResult> GetDevice(string serial, CancellationToken ct)
    {
        var device = await _service.GetDeviceAsync(serial, ct);
        return device == null ? NotFound() : Ok(device);
    }

    [HttpPut("devices/{serial}")]
    public async Task<IActionResult> UpdateDevice(string serial, [FromBody] MycoBrainDevice device, CancellationToken ct)
    {
        if (!string.Equals(serial, device.DeviceId, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "serial_mismatch" });

        return Ok(await _service.UpdateDeviceAsync(device, ct));
    }

    [HttpGet("devices/{serial}/telemetry")]
    public async Task<IActionResult> TelemetryHistory(string serial, [FromQuery] DateTime? startTime, [FromQuery] DateTime? endTime, CancellationToken ct)
    {
        return Ok(await _service.GetTelemetryHistoryAsync(serial, startTime, endTime, ct));
    }
}
