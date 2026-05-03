using Microsoft.AspNetCore.Mvc;
using NatureOS.CoreApi.Services;
using NatureOS.MINDEX.Models;
using System.Text.Json;

namespace NatureOS.CoreApi.Controllers;

[ApiController]
[Route("api/mycobrain")]
public class MycoBrainController : ControllerBase
{
    private readonly IMycoBrainService _service;
    private readonly DeepAgentEventService _deepAgentEvents;
    private readonly ILogger<MycoBrainController> _logger;

    public MycoBrainController(
        IMycoBrainService service,
        DeepAgentEventService deepAgentEvents,
        ILogger<MycoBrainController> logger)
    {
        _service = service;
        _deepAgentEvents = deepAgentEvents;
        _logger = logger;
    }

    [HttpPost("telemetry")]
    public async Task<IActionResult> Telemetry([FromBody] MycoBrainTelemetry telemetry, CancellationToken ct)
    {
        var result = await _service.ProcessTelemetryAsync(telemetry, ct);
        await _deepAgentEvents.PublishAsync(
            domain: "device",
            task: $"NatureOS telemetry processed for {telemetry.SerialNumber}",
            context: new { route = "/api/mycobrain/telemetry", serial = telemetry.SerialNumber },
            preferredAgent: "ops-agent",
            cancellationToken: ct);
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

    [HttpPost("telemetry/envelope")]
    public async Task<IActionResult> TelemetryEnvelope([FromBody] JsonElement envelope, CancellationToken ct)
    {
        var result = await _service.ProcessEnvelopeAsync(envelope, ct);
        return Ok(result);
    }

    [HttpPost("command")]
    public async Task<IActionResult> SendCommand([FromBody] MycoBrainCommand command, CancellationToken ct)
    {
        var ok = await _service.SendCommandAsync(command, ct);
        if (ok)
        {
            await _deepAgentEvents.PublishAsync(
                domain: "device",
                task: $"NatureOS MycoBrain command sent: {command.CommandId}",
                context: new { route = "/api/mycobrain/command", target = command.TargetSerial, sequence = command.SequenceNumber },
                preferredAgent: "ops-agent",
                cancellationToken: ct);
        }
        return ok ? Ok(new { status = "sent" }) : StatusCode(500, new { error = "send_failed" });
    }

    [HttpPost("devices/register")]
    public async Task<IActionResult> Register([FromBody] MycoBrainDevice device, CancellationToken ct)
    {
        var saved = await _service.RegisterDeviceAsync(device, ct);
        await _deepAgentEvents.PublishAsync(
            domain: "device",
            task: $"NatureOS MycoBrain device registered: {device.DeviceId}",
            context: new { route = "/api/mycobrain/devices/register", deviceId = device.DeviceId, deviceType = device.DeviceType },
            preferredAgent: "ops-agent",
            cancellationToken: ct);
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
