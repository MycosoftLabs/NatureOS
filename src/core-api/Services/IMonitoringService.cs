namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service interface for system health, metrics, and alerts
/// </summary>
public interface IMonitoringService
{
    /// <summary>
    /// Get current system metrics (CPU, memory, disk)
    /// </summary>
    Task<SystemMetrics> GetSystemMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recent system alerts
    /// </summary>
    Task<IEnumerable<SystemAlert>> GetAlertsAsync(int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get overall system health status
    /// </summary>
    Task<SystemHealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get device health metrics
    /// </summary>
    Task<DeviceHealthSummary> GetDeviceHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Overall system health status
/// </summary>
public class SystemHealthStatus
{
    public string Status { get; set; } = "Healthy"; // Healthy, Degraded, Unhealthy
    public DateTime Timestamp { get; set; }
    public Dictionary<string, string> Checks { get; set; } = new();
    public int ActiveAlerts { get; set; }
}

/// <summary>
/// Device health summary
/// </summary>
public class DeviceHealthSummary
{
    public int TotalDevices { get; set; }
    public int OnlineCount { get; set; }
    public int OfflineCount { get; set; }
    public int LowBatteryCount { get; set; }
}
