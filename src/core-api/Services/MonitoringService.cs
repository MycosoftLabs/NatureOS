using System.Globalization;

namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service for system health, metrics, and alerts
/// </summary>
public class MonitoringService : IMonitoringService
{
    private readonly IDeviceService _deviceService;
    private readonly ILogger<MonitoringService> _logger;
    private readonly List<SystemAlert> _alerts = new();
    private static readonly object _alertsLock = new();

    public MonitoringService(IDeviceService deviceService, ILogger<MonitoringService> logger)
    {
        _deviceService = deviceService;
        _logger = logger;
    }

    public Task<SystemMetrics> GetSystemMetricsAsync(CancellationToken cancellationToken = default)
    {
        var metrics = new SystemMetrics { Timestamp = DateTime.UtcNow };
        try
        {
            metrics.CpuUsage = TryGetCpuUsage();
            metrics.MemoryUsage = TryGetMemoryUsage();
            metrics.DiskUsage = TryGetDiskUsage();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to gather some system metrics");
        }
        return Task.FromResult(metrics);
    }

    public Task<IEnumerable<SystemAlert>> GetAlertsAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        lock (_alertsLock)
        {
            return Task.FromResult(_alerts.Take(limit).AsEnumerable());
        }
    }

    public async Task<SystemHealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken = default)
    {
        var metrics = await GetSystemMetricsAsync(cancellationToken);
        var status = "Healthy";
        var checks = new Dictionary<string, string>();

        if (metrics.CpuUsage.HasValue)
        {
            checks["cpu"] = metrics.CpuUsage > 90 ? "Critical" : metrics.CpuUsage > 80 ? "Warning" : "Healthy";
            if (metrics.CpuUsage > 90) status = "Degraded";
        }
        if (metrics.MemoryUsage.HasValue)
        {
            checks["memory"] = metrics.MemoryUsage > 95 ? "Critical" : metrics.MemoryUsage > 85 ? "Warning" : "Healthy";
            if (metrics.MemoryUsage > 95) status = "Degraded";
        }
        if (metrics.DiskUsage.HasValue)
        {
            checks["disk"] = metrics.DiskUsage > 95 ? "Critical" : metrics.DiskUsage > 90 ? "Warning" : "Healthy";
            if (metrics.DiskUsage > 95) status = "Degraded";
        }

        return new SystemHealthStatus
        {
            Status = status,
            Timestamp = DateTime.UtcNow,
            Checks = checks,
            ActiveAlerts = 0
        };
    }

    public async Task<DeviceHealthSummary> GetDeviceHealthAsync(CancellationToken cancellationToken = default)
    {
        var devices = (await _deviceService.GetDevicesAsync(cancellationToken)).ToList();
        return new DeviceHealthSummary
        {
            TotalDevices = devices.Count,
            OnlineCount = devices.Count(d => d.Status == DeviceStatus.Online),
            OfflineCount = devices.Count(d => d.Status == DeviceStatus.Offline),
            LowBatteryCount = devices.Count(d => d.BatteryLevel.HasValue && d.BatteryLevel < 20)
        };
    }

    private static double? TryGetCpuUsage()
    {
        try
        {
            if (OperatingSystem.IsLinux() && File.Exists("/proc/stat"))
            {
                var line = File.ReadLines("/proc/stat").FirstOrDefault(l => l.StartsWith("cpu "));
                if (line != null)
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5 && long.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var idle))
                    {
                        var total = 0L;
                        for (var i = 1; i < Math.Min(parts.Length, 10); i++)
                            if (long.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                                total += v;
                        if (total > 0)
                            return (double)(total - idle) / total * 100.0;
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private static double? TryGetMemoryUsage()
    {
        try
        {
            var info = GC.GetGCMemoryInfo();
            if (info.TotalAvailableMemoryBytes > 0)
            {
                var used = info.TotalAvailableMemoryBytes - info.MemoryLoadBytes;
                return (double)used / info.TotalAvailableMemoryBytes * 100.0;
            }
        }
        catch { }
        return null;
    }

    private static double? TryGetDiskUsage()
    {
        try
        {
            var root = Path.GetPathRoot(AppContext.BaseDirectory);
            if (!string.IsNullOrWhiteSpace(root))
            {
                var drive = new DriveInfo(root);
                if (drive.IsReady && drive.TotalSize > 0)
                    return (double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100.0;
            }
        }
        catch { }
        return null;
    }
}
