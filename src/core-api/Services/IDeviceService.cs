using NatureOS.MINDEX.Models;

namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service interface for managing IoT devices
/// </summary>
public interface IDeviceService
{
    /// <summary>
    /// Register a new device
    /// </summary>
    /// <param name="device">Device to register</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The registered device</returns>
    Task<Device> RegisterDeviceAsync(Device device, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a device by ID
    /// </summary>
    /// <param name="deviceId">Device ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The device if found</returns>
    Task<Device?> GetDeviceAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all devices
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of devices</returns>
    Task<IEnumerable<Device>> GetDevicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Update device information
    /// </summary>
    /// <param name="device">Updated device information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated device</returns>
    Task<Device> UpdateDeviceAsync(Device device, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a device
    /// </summary>
    /// <param name="deviceId">Device ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> DeleteDeviceAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get device statistics
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Device statistics</returns>
    Task<DeviceStatistics> GetDeviceStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Device model
/// </summary>
public class Device
{
    /// <summary>
    /// Device ID (EUI-64 or similar)
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Device name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Device type (e.g., "mushroom-sensor", "spore-detector")
    /// </summary>
    public string DeviceType { get; set; } = string.Empty;

    /// <summary>
    /// Device location
    /// </summary>
    public GeoLocation? Location { get; set; }

    /// <summary>
    /// Device status
    /// </summary>
    public DeviceStatus Status { get; set; } = DeviceStatus.Unknown;

    /// <summary>
    /// Last seen timestamp
    /// </summary>
    public DateTime? LastSeen { get; set; }

    /// <summary>
    /// Battery level percentage (0-100)
    /// </summary>
    public int? BatteryLevel { get; set; }

    /// <summary>
    /// Tenant ID
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Device metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Updated timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Device status enumeration
/// </summary>
public enum DeviceStatus
{
    Unknown,
    Online,
    Offline,
    Maintenance,
    Error
}

/// <summary>
/// Device statistics
/// </summary>
public class DeviceStatistics
{
    /// <summary>
    /// Total device count
    /// </summary>
    public long TotalDevices { get; set; }

    /// <summary>
    /// Total count (alias for TotalDevices)
    /// </summary>
    public long TotalCount => TotalDevices;

    /// <summary>
    /// Online device count
    /// </summary>
    public long OnlineCount { get; set; }

    /// <summary>
    /// Devices by status
    /// </summary>
    public Dictionary<DeviceStatus, long> DevicesByStatus { get; set; } = new();

    /// <summary>
    /// Devices by type
    /// </summary>
    public Dictionary<string, long> DevicesByType { get; set; } = new();

    /// <summary>
    /// Active devices (seen in last 24 hours)
    /// </summary>
    public long ActiveDevices { get; set; }
} 