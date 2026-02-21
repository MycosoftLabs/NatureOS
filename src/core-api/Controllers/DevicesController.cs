using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NatureOS.CoreApi.Services;
using NatureOS.MINDEX.Models;

namespace NatureOS.CoreApi.Controllers;

/// <summary>
/// API controller for managing IoT devices
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DevicesController : ControllerBase
{
    private readonly IDeviceService _deviceService;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(IDeviceService deviceService, ILogger<DevicesController> logger)
    {
        _deviceService = deviceService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new device (MAS compatibility alias)
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(Device), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public Task<ActionResult<Device>> RegisterDeviceAlias(
        [FromBody] Device device,
        CancellationToken cancellationToken = default) =>
        RegisterDevice(device, cancellationToken);

    /// <summary>
    /// Register a new device
    /// </summary>
    /// <param name="device">Device to register</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The registered device</returns>
    [HttpPost]
    [ProducesResponseType(typeof(Device), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<Device>> RegisterDevice(
        [FromBody] Device device,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate required fields
            if (string.IsNullOrEmpty(device.DeviceId))
            {
                return BadRequest("DeviceId is required");
            }

            if (string.IsNullOrEmpty(device.Name))
            {
                return BadRequest("Name is required");
            }

            if (string.IsNullOrEmpty(device.DeviceType))
            {
                return BadRequest("DeviceType is required");
            }

            var registeredDevice = await _deviceService.RegisterDeviceAsync(device, cancellationToken);
            
            return CreatedAtAction(
                nameof(GetDevice),
                new { deviceId = registeredDevice.DeviceId },
                registeredDevice);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register device");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get a device by ID
    /// </summary>
    /// <param name="deviceId">Device ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The device if found</returns>
    [HttpGet("{deviceId}")]
    [ProducesResponseType(typeof(Device), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<Device>> GetDevice(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var device = await _deviceService.GetDeviceAsync(deviceId, cancellationToken);
            
            if (device == null)
            {
                return NotFound($"Device {deviceId} not found");
            }

            return Ok(device);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get device {DeviceId}", deviceId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get all devices
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of devices</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Device>), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<Device>>> GetDevices(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var devices = await _deviceService.GetDevicesAsync(cancellationToken);
            return Ok(devices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get devices");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update device information
    /// </summary>
    /// <param name="deviceId">Device ID</param>
    /// <param name="device">Updated device information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated device</returns>
    [HttpPut("{deviceId}")]
    [ProducesResponseType(typeof(Device), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<Device>> UpdateDevice(
        string deviceId,
        [FromBody] Device device,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (deviceId != device.DeviceId)
            {
                return BadRequest("Device ID in URL must match device ID in body");
            }

            var updatedDevice = await _deviceService.UpdateDeviceAsync(device, cancellationToken);
            return Ok(updatedDevice);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update device {DeviceId}", deviceId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete a device
    /// </summary>
    /// <param name="deviceId">Device ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpDelete("{deviceId}")]
    [Authorize]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult> DeleteDevice(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deleted = await _deviceService.DeleteDeviceAsync(deviceId, cancellationToken);
            
            if (!deleted)
            {
                return NotFound($"Device {deviceId} not found");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete device {DeviceId}", deviceId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get device statistics
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Device statistics</returns>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(DeviceStatistics), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<DeviceStatistics>> GetDeviceStatistics(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var statistics = await _deviceService.GetDeviceStatisticsAsync(cancellationToken);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get device statistics");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get devices by status
    /// </summary>
    /// <param name="status">Device status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of devices with the specified status</returns>
    [HttpGet("by-status/{status}")]
    [ProducesResponseType(typeof(IEnumerable<Device>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<Device>>> GetDevicesByStatus(
        string status,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Enum.TryParse<DeviceStatus>(status, true, out var deviceStatus))
            {
                return BadRequest($"Invalid device status: {status}");
            }

            var devices = await _deviceService.GetDevicesAsync(cancellationToken);
            var filteredDevices = devices.Where(d => d.Status == deviceStatus);
            
            return Ok(filteredDevices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get devices by status {Status}", status);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get devices by type
    /// </summary>
    /// <param name="deviceType">Device type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of devices with the specified type</returns>
    [HttpGet("by-type/{deviceType}")]
    [ProducesResponseType(typeof(IEnumerable<Device>), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<Device>>> GetDevicesByType(
        string deviceType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var devices = await _deviceService.GetDevicesAsync(cancellationToken);
            var filteredDevices = devices.Where(d => 
                string.Equals(d.DeviceType, deviceType, StringComparison.OrdinalIgnoreCase));
            
            return Ok(filteredDevices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get devices by type {DeviceType}", deviceType);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update device status
    /// </summary>
    /// <param name="deviceId">Device ID</param>
    /// <param name="status">New status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpPatch("{deviceId}/status")]
    [ProducesResponseType(typeof(Device), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<Device>> UpdateDeviceStatus(
        string deviceId,
        [FromBody] DeviceStatusUpdate statusUpdate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Enum.TryParse<DeviceStatus>(statusUpdate.Status, true, out var deviceStatus))
            {
                return BadRequest($"Invalid device status: {statusUpdate.Status}");
            }

            var device = await _deviceService.GetDeviceAsync(deviceId, cancellationToken);
            if (device == null)
            {
                return NotFound($"Device {deviceId} not found");
            }

            device.Status = deviceStatus;
            device.LastSeen = DateTime.UtcNow;
            
            var updatedDevice = await _deviceService.UpdateDeviceAsync(device, cancellationToken);
            return Ok(updatedDevice);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update device status for {DeviceId}", deviceId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get active devices (seen in last 24 hours)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active devices</returns>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<Device>), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<Device>>> GetActiveDevices(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var devices = await _deviceService.GetDevicesAsync(cancellationToken);
            var cutoff = DateTime.UtcNow.AddDays(-1);
            var activeDevices = devices.Where(d => d.LastSeen >= cutoff);
            
            return Ok(activeDevices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active devices");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// MAS compatibility: Get sensor data for a device
    /// </summary>
    [HttpGet("{deviceId}/sensor-data")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> GetSensorData(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var device = await _deviceService.GetDeviceAsync(deviceId, cancellationToken);
            if (device == null)
                return NotFound($"Device {deviceId} not found");

            var sensorData = new
            {
                deviceId = device.DeviceId,
                timestamp = device.LastSeen ?? DateTime.UtcNow,
                status = device.Status.ToString(),
                batteryLevel = device.BatteryLevel,
                location = device.Location,
                metadata = device.Metadata ?? new Dictionary<string, object>(),
            };
            return Ok(sensorData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get sensor data for {DeviceId}", deviceId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// MAS compatibility: Send MycoBrain command to device
    /// </summary>
    [HttpPost("{deviceId}/commands/mycobrain")]
    [ProducesResponseType(202)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> SendMycoBrainCommand(
        string deviceId,
        [FromBody] object? command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var device = await _deviceService.GetDeviceAsync(deviceId, cancellationToken);
            if (device == null)
                return NotFound($"Device {deviceId} not found");

            _logger.LogInformation("MycoBrain command received for {DeviceId}", deviceId);
            return Accepted(new
            {
                deviceId,
                status = "accepted",
                message = "Command queued for MycoBrain device",
                receivedAt = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send MycoBrain command to {DeviceId}", deviceId);
            return StatusCode(500, "Internal server error");
        }
    }
}

/// <summary>
/// Device status update model
/// </summary>
public class DeviceStatusUpdate
{
    /// <summary>
    /// New device status
    /// </summary>
    public string Status { get; set; } = string.Empty;
} 