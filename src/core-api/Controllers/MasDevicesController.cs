using Microsoft.AspNetCore.Mvc;
using NatureOS.CoreApi.Services;
using NatureOS.MINDEX.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NatureOS.CoreApi.Controllers;

/// <summary>
/// Compatibility endpoints for MAS NATUREOSClient
/// </summary>
[ApiController]
[Route("devices")]
[Produces("application/json")]
public class MasDevicesController : ControllerBase
{
    private readonly IDeviceService _deviceService;
    private readonly IMycoBrainService _mycoBrainService;
    private readonly ILogger<MasDevicesController> _logger;

    public MasDevicesController(
        IDeviceService deviceService,
        IMycoBrainService mycoBrainService,
        ILogger<MasDevicesController> logger)
    {
        _deviceService = deviceService;
        _mycoBrainService = mycoBrainService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Device>), 200)]
    public async Task<IActionResult> GetDevices(CancellationToken cancellationToken = default)
    {
        var devices = (await _deviceService.GetDevicesAsync(cancellationToken)).ToList();
        var mycoDevices = await _mycoBrainService.GetDevicesAsync(cancellationToken);

        var merged = devices.ToDictionary(d => d.DeviceId, StringComparer.OrdinalIgnoreCase);
        foreach (var mycoDevice in mycoDevices)
        {
            if (!merged.ContainsKey(mycoDevice.DeviceId))
                merged[mycoDevice.DeviceId] = MapMycoBrainDevice(mycoDevice);
        }

        return Ok(new { items = merged.Values });
    }

    [HttpGet("{deviceId}")]
    [ProducesResponseType(typeof(Device), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetDevice(string deviceId, CancellationToken cancellationToken = default)
    {
        var device = await _deviceService.GetDeviceAsync(deviceId, cancellationToken);
        if (device != null)
            return Ok(device);

        var mycoDevice = await _mycoBrainService.GetDeviceAsync(deviceId, cancellationToken);
        return mycoDevice == null
            ? NotFound(new { error = "device_not_found" })
            : Ok(MapMycoBrainDevice(mycoDevice));
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(Device), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> RegisterDevice([FromBody] Device device, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(device.DeviceId) ||
            string.IsNullOrWhiteSpace(device.Name) ||
            string.IsNullOrWhiteSpace(device.DeviceType))
        {
            return BadRequest(new { error = "missing_required_fields" });
        }

        var registeredDevice = await _deviceService.RegisterDeviceAsync(device, cancellationToken);
        return CreatedAtAction(nameof(GetDevice), new { deviceId = registeredDevice.DeviceId }, registeredDevice);
    }

    [HttpPut("{deviceId}/config")]
    [ProducesResponseType(typeof(Device), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateDeviceConfig(
        string deviceId,
        [FromBody] Dictionary<string, object> config,
        CancellationToken cancellationToken = default)
    {
        var device = await _deviceService.GetDeviceAsync(deviceId, cancellationToken);
        if (device == null)
        {
            var mycoDevice = await _mycoBrainService.GetDeviceAsync(deviceId, cancellationToken);
            if (mycoDevice == null)
                return NotFound(new { error = "device_not_found" });
        }

        device.Metadata ??= new Dictionary<string, object>();
        foreach (var entry in config)
        {
            device.Metadata[entry.Key] = entry.Value;
        }

        var updated = await _deviceService.UpdateDeviceAsync(device, cancellationToken);
        return Ok(updated);
    }

    [HttpGet("{deviceId}/sensor-data")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetSensorData(
        string deviceId,
        [FromQuery(Name = "sensor_type")] string? sensorType,
        [FromQuery(Name = "start_time")] DateTime? startTime,
        [FromQuery(Name = "end_time")] DateTime? endTime,
        [FromQuery] int? limit,
        CancellationToken cancellationToken = default)
    {
        var device = await _deviceService.GetDeviceAsync(deviceId, cancellationToken);
        if (device == null)
            return NotFound(new { error = "device_not_found" });

        var telemetry = await _mycoBrainService.GetTelemetryHistoryAsync(deviceId, startTime, endTime, cancellationToken);
        var readings = BuildSensorReadings(deviceId, telemetry, sensorType, limit ?? 1000);

        return Ok(new { items = readings });
    }

    [HttpPost("{deviceId}/commands/mycobrain")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> SendMycoBrainCommand(
        string deviceId,
        [FromBody] MasMycoBrainCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.CommandType))
            return BadRequest(new { error = "command_type_required" });

        if (!TryMapCommand(request.CommandType, out var commandId))
            return BadRequest(new { error = "unknown_command_type" });

        var sequence = ExtractSequenceNumber(request.Parameters) ?? (uint)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & 0xFFFFFFFF);
        var parameters = ConvertParameters(request.Parameters);

        var command = new MycoBrainCommand
        {
            CommandId = commandId,
            SequenceNumber = sequence,
            TargetSerial = deviceId,
            Parameters = parameters
        };

        var ok = await _mycoBrainService.SendCommandAsync(command, cancellationToken);
        if (!ok)
        {
            _logger.LogError("Failed to send MycoBrain command {CommandId} to {DeviceId}", commandId, deviceId);
            return StatusCode(500, new { error = "send_failed" });
        }

        return Ok(new { status = "sent", sequence });
    }

    private static List<SensorReading> BuildSensorReadings(
        string deviceId,
        IEnumerable<MycoBrainTelemetry> telemetry,
        string? sensorType,
        int limit)
    {
        var normalizedFilter = NormalizeSensorType(sensorType);
        var readings = new List<SensorReading>();

        foreach (var entry in telemetry)
        {
            var ts = DateTime.UnixEpoch.AddMilliseconds(entry.DeviceTimestamp);
            var bme = entry.SideA?.BME688;
            if (bme == null)
                continue;

            AddReadingIfMatch(readings, normalizedFilter, "temperature", bme.Temperature, ts, deviceId, entry);
            AddReadingIfMatch(readings, normalizedFilter, "humidity", bme.Humidity, ts, deviceId, entry);
            AddReadingIfMatch(readings, normalizedFilter, "pressure", bme.Pressure, ts, deviceId, entry);
            AddReadingIfMatch(readings, normalizedFilter, "gas_resistance", bme.GasResistance, ts, deviceId, entry);
        }

        return readings
            .OrderByDescending(r => r.Timestamp)
            .Take(Math.Max(0, limit))
            .ToList();
    }

    private static void AddReadingIfMatch(
        List<SensorReading> readings,
        string? filter,
        string sensorType,
        double value,
        DateTime timestamp,
        string deviceId,
        MycoBrainTelemetry telemetry)
    {
        if (!string.IsNullOrEmpty(filter) && !string.Equals(filter, sensorType, StringComparison.OrdinalIgnoreCase))
            return;

        readings.Add(new SensorReading
        {
            DeviceId = deviceId,
            SensorType = sensorType,
            Value = value,
            Unit = null,
            Timestamp = timestamp,
            Metadata = new Dictionary<string, object?>
            {
                ["serial"] = telemetry.SerialNumber,
                ["sequence"] = telemetry.SequenceNumber,
                ["firmware_version"] = telemetry.FirmwareVersion
            }
        });
    }

    private static string? NormalizeSensorType(string? sensorType)
    {
        if (string.IsNullOrWhiteSpace(sensorType))
            return null;

        return sensorType.Trim().ToLowerInvariant().Replace(" ", "_");
    }

    private static bool TryMapCommand(string commandType, out MycoBrainCommandId commandId)
    {
        var normalized = commandType.Trim().ToLowerInvariant();
        return normalized switch
        {
            "set_mosfet" => Set(out commandId, MycoBrainCommandId.SetMosfet),
            "set_telemetry_interval" => Set(out commandId, MycoBrainCommandId.SetTelemetryInterval),
            "i2c_scan" => Set(out commandId, MycoBrainCommandId.ScanI2C),
            "get_status" => Set(out commandId, MycoBrainCommandId.GetStatus),
            "set_analog_label" => Set(out commandId, MycoBrainCommandId.SetAnalogLabel),
            "set_mosfet_label" => Set(out commandId, MycoBrainCommandId.SetMosfetLabel),
            "firmware_update" => Set(out commandId, MycoBrainCommandId.FirmwareUpdate),
            "reset" => Set(out commandId, MycoBrainCommandId.Reset),
            _ => Set(out commandId, default, false)
        };
    }

    private static bool Set(out MycoBrainCommandId target, MycoBrainCommandId value, bool result = true)
    {
        target = value;
        return result;
    }

    private static uint? ExtractSequenceNumber(Dictionary<string, JsonElement>? parameters)
    {
        if (parameters == null) return null;

        foreach (var key in new[] { "seq", "sequence", "sequence_number" })
        {
            if (!parameters.TryGetValue(key, out var element)) continue;
            if (element.ValueKind == JsonValueKind.Number && element.TryGetUInt32(out var number))
                return number;
        }

        return null;
    }

    private static Dictionary<string, object>? ConvertParameters(Dictionary<string, JsonElement>? parameters)
    {
        if (parameters == null || parameters.Count == 0)
            return null;

        var output = new Dictionary<string, object>();
        foreach (var entry in parameters)
        {
            output[entry.Key] = entry.Value;
        }

        return output;
    }

    private static Device MapMycoBrainDevice(MycoBrainDevice device)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["firmware_version"] = device.FirmwareVersion,
            ["i2c_addresses"] = device.I2CAddresses,
            ["analog_labels"] = device.AnalogLabels,
            ["mosfet_labels"] = device.MosfetLabels,
            ["power_status"] = device.PowerStatus
        };

        return new Device
        {
            DeviceId = device.DeviceId,
            Name = device.DeviceId,
            DeviceType = device.DeviceType,
            Location = device.Location,
            Status = MapStatus(device.Status),
            LastSeen = device.LastSeen,
            Metadata = metadata
        };
    }

    private static NatureOS.CoreApi.Services.DeviceStatus MapStatus(NatureOS.MINDEX.Models.DeviceStatus status)
    {
        return status switch
        {
            NatureOS.MINDEX.Models.DeviceStatus.Online => NatureOS.CoreApi.Services.DeviceStatus.Online,
            NatureOS.MINDEX.Models.DeviceStatus.Offline => NatureOS.CoreApi.Services.DeviceStatus.Offline,
            NatureOS.MINDEX.Models.DeviceStatus.Maintenance => NatureOS.CoreApi.Services.DeviceStatus.Maintenance,
            NatureOS.MINDEX.Models.DeviceStatus.Error => NatureOS.CoreApi.Services.DeviceStatus.Error,
            _ => NatureOS.CoreApi.Services.DeviceStatus.Unknown
        };
    }
}

public sealed class MasMycoBrainCommandRequest
{
    [JsonPropertyName("command_type")]
    public string CommandType { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public Dictionary<string, JsonElement>? Parameters { get; set; }
}

public sealed class SensorReading
{
    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("sensor_type")]
    public string SensorType { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; set; }
}
