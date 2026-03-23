using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NatureOS.CoreApi.Hubs;
using System.Diagnostics;
using System.Globalization;

namespace NatureOS.CoreApi.Services;

/// <summary>
/// Background service for proactive system monitoring and alerting
/// </summary>
public class ProactiveMonitoringService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<NatureOSHub> _hubContext;
    private readonly ILogger<ProactiveMonitoringService> _logger;
    private readonly IConfiguration _configuration;
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _memoryCounter;
    private long? _lastCpuTotal;
    private long? _lastCpuIdle;

    public ProactiveMonitoringService(
        IServiceProvider serviceProvider,
        IHubContext<NatureOSHub> hubContext,
        ILogger<ProactiveMonitoringService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
        _configuration = configuration;

        try
        {
            // Initialize performance counters (Windows only)
            if (OperatingSystem.IsWindows())
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize performance counters");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Proactive monitoring service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformMonitoringCycle();
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in monitoring cycle");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait longer on error
            }
        }

        _logger.LogInformation("Proactive monitoring service stopped");
    }

    private async Task PerformMonitoringCycle()
    {
        var tasks = new List<Task>
        {
            CheckSystemHealth(),
            CheckDeviceHealth(),
            CheckDataQuality(),
            CheckApiPerformance(),
            CheckExternalConnectivity()
        };

        await Task.WhenAll(tasks);
    }

    private async Task CheckSystemHealth()
    {
        try
        {
            var systemMetrics = await GatherSystemMetrics();
            
            // Check CPU usage
            if (systemMetrics.CpuUsage.HasValue && systemMetrics.CpuUsage > 80)
            {
                await SendAlert(new SystemAlert
                {
                    Type = "SystemHealth",
                    Category = "Performance",
                    Severity = systemMetrics.CpuUsage > 90 ? "Critical" : "Warning",
                    Message = $"High CPU usage detected: {systemMetrics.CpuUsage:F1}%",
                    Timestamp = DateTime.UtcNow,
                    Recommendations = new[]
                    {
                        "Check for resource-intensive processes",
                        "Consider scaling up resources",
                        "Review recent deployments"
                    }
                });
            }

            // Check memory usage
            if (systemMetrics.MemoryUsage.HasValue && systemMetrics.MemoryUsage > 85)
            {
                await SendAlert(new SystemAlert
                {
                    Type = "SystemHealth",
                    Category = "Memory",
                    Severity = systemMetrics.MemoryUsage > 95 ? "Critical" : "Warning",
                    Message = $"High memory usage detected: {systemMetrics.MemoryUsage:F1}%",
                    Timestamp = DateTime.UtcNow,
                    Recommendations = new[]
                    {
                        "Check for memory leaks",
                        "Review cache configurations",
                        "Consider scaling up memory"
                    }
                });
            }

            // Check disk space
            if (systemMetrics.DiskUsage.HasValue && systemMetrics.DiskUsage > 90)
            {
                await SendAlert(new SystemAlert
                {
                    Type = "SystemHealth",
                    Category = "Storage",
                    Severity = "Critical",
                    Message = $"Low disk space: {systemMetrics.DiskUsage:F1}% used",
                    Timestamp = DateTime.UtcNow,
                    Recommendations = new[]
                    {
                        "Clean up temporary files",
                        "Archive old logs",
                        "Increase storage capacity"
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking system health");
        }
    }

    private async Task CheckDeviceHealth()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
            
            var devices = await deviceService.GetDevicesAsync();
            var offlineDevices = devices.Where(d => d.Status == DeviceStatus.Offline || 
                (d.LastSeen.HasValue && (DateTime.UtcNow - d.LastSeen.Value).TotalMinutes > 30));
            
            foreach (var device in offlineDevices)
            {
                await SendAlert(new SystemAlert
                {
                    Type = "DeviceHealth",
                    Category = "Connectivity",
                    Severity = "Warning",
                    Message = $"Device {device.DeviceId} appears offline",
                    Timestamp = DateTime.UtcNow,
                    DeviceId = device.DeviceId,
                    Recommendations = new[]
                    {
                        "Check device power and connectivity",
                        "Verify network connection",
                        "Review device logs"
                    }
                });
            }

            // Check for devices with low battery
            var lowBatteryDevices = devices.Where(d => d.BatteryLevel.HasValue && d.BatteryLevel < 20);
            foreach (var device in lowBatteryDevices)
            {
                await SendAlert(new SystemAlert
                {
                    Type = "DeviceHealth",
                    Category = "Battery",
                    Severity = device.BatteryLevel < 10 ? "Critical" : "Warning",
                    Message = $"Low battery on device {device.DeviceId}: {device.BatteryLevel}%",
                    Timestamp = DateTime.UtcNow,
                    DeviceId = device.DeviceId,
                    Recommendations = new[]
                    {
                        "Schedule battery replacement",
                        "Check power management settings",
                        "Consider backup power options"
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking device health");
        }
    }

    private async Task CheckDataQuality()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
            
            var recentEvents = await eventService.GetEventsAsync(new EventQuery
            {
                StartDate = DateTime.UtcNow.AddHours(-1),
                Limit = 1000
            });

            var eventCount = recentEvents.Items.Count();
            var stats = await eventService.GetEventStatisticsAsync(new EventQuery
            {
                StartDate = DateTime.UtcNow.AddHours(-24),
                EndTime = DateTime.UtcNow
            });
            var averageEventsPerHour = stats.AveragePerHour;

            if (averageEventsPerHour <= 0)
                return;

            // Check for unusual data patterns
            if (eventCount < averageEventsPerHour * 0.5) // Less than 50% of normal
            {
                await SendAlert(new SystemAlert
                {
                    Type = "DataQuality",
                    Category = "Volume",
                    Severity = "Warning",
                    Message = $"Unusually low data volume: {eventCount} events in last hour (baseline {averageEventsPerHour:F1}/hour)",
                    Timestamp = DateTime.UtcNow,
                    Recommendations = new[]
                    {
                        "Check device connectivity",
                        "Verify data ingestion pipeline",
                        "Review processing logs"
                    }
                });
            }
            else if (eventCount > averageEventsPerHour * 2) // More than 200% of normal
            {
                await SendAlert(new SystemAlert
                {
                    Type = "DataQuality",
                    Category = "Volume",
                    Severity = "Info",
                    Message = $"Unusually high data volume: {eventCount} events in last hour (baseline {averageEventsPerHour:F1}/hour)",
                    Timestamp = DateTime.UtcNow,
                    Recommendations = new[]
                    {
                        "Monitor system performance",
                        "Check for data quality issues",
                        "Verify device configurations"
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking data quality");
        }
    }

    private async Task CheckApiPerformance()
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Test API responsiveness
            using var scope = _serviceProvider.CreateScope();
            var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
            
            await eventService.GetEventsAsync(new EventQuery { Limit = 1 });
            stopwatch.Stop();

            var responseTime = stopwatch.ElapsedMilliseconds;
            
            if (responseTime > 5000) // More than 5 seconds
            {
                await SendAlert(new SystemAlert
                {
                    Type = "Performance",
                    Category = "API",
                    Severity = "Critical",
                    Message = $"API response time is critically slow: {responseTime}ms",
                    Timestamp = DateTime.UtcNow,
                    Recommendations = new[]
                    {
                        "Check database performance",
                        "Review system resources",
                        "Consider scaling infrastructure"
                    }
                });
            }
            else if (responseTime > 2000) // More than 2 seconds
            {
                await SendAlert(new SystemAlert
                {
                    Type = "Performance",
                    Category = "API",
                    Severity = "Warning",
                    Message = $"API response time is slower than normal: {responseTime}ms",
                    Timestamp = DateTime.UtcNow,
                    Recommendations = new[]
                    {
                        "Monitor database queries",
                        "Check cache performance",
                        "Review recent changes"
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking API performance");
            
            await SendAlert(new SystemAlert
            {
                Type = "Performance",
                Category = "API",
                Severity = "Critical",
                Message = "API health check failed",
                Timestamp = DateTime.UtcNow,
                Recommendations = new[]
                {
                    "Check service availability",
                    "Review error logs",
                    "Verify dependencies"
                }
            });
        }
    }

    private async Task CheckExternalConnectivity()
    {
        try
        {
            // Test external database connections
            var externalTests = new Dictionary<string, Func<Task<bool>>>
            {
                ["CosmosDB"] = TestCosmosDbConnectivity,
                ["EventGrid"] = TestEventGridConnectivity,
                ["ServiceBus"] = TestServiceBusConnectivity
            };

            foreach (var test in externalTests)
            {
                try
                {
                    var isHealthy = await test.Value();
                    if (!isHealthy)
                    {
                        await SendAlert(new SystemAlert
                        {
                            Type = "Connectivity",
                            Category = "External",
                            Severity = "Warning",
                            Message = $"{test.Key} connectivity issue detected",
                            Timestamp = DateTime.UtcNow,
                            Recommendations = new[]
                            {
                                $"Check {test.Key} service status",
                                "Verify network connectivity",
                                "Review authentication credentials"
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to test {Service} connectivity", test.Key);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking external connectivity");
        }
    }

    private async Task<SystemMetrics> GatherSystemMetrics()
    {
        var metrics = new SystemMetrics
        {
            Timestamp = DateTime.UtcNow
        };

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

        return metrics;
    }

    private double? TryGetCpuUsage()
    {
        if (_cpuCounter != null)
        {
            var value = _cpuCounter.NextValue();
            return value >= 0 ? value : null;
        }

        if (OperatingSystem.IsLinux())
            return TryGetLinuxCpuUsage();

        return null;
    }

    private double? TryGetLinuxCpuUsage()
    {
        try
        {
            if (!File.Exists("/proc/stat"))
                return null;

            var line = File.ReadLines("/proc/stat").FirstOrDefault(l => l.StartsWith("cpu "));
            if (line == null)
                return null;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
                return null;

            long ReadAt(int index) => long.Parse(parts[index], CultureInfo.InvariantCulture);

            var user = ReadAt(1);
            var nice = ReadAt(2);
            var system = ReadAt(3);
            var idle = ReadAt(4);
            var iowait = parts.Length > 5 ? ReadAt(5) : 0;
            var irq = parts.Length > 6 ? ReadAt(6) : 0;
            var softirq = parts.Length > 7 ? ReadAt(7) : 0;
            var steal = parts.Length > 8 ? ReadAt(8) : 0;

            var total = user + nice + system + idle + iowait + irq + softirq + steal;
            var idleAll = idle + iowait;

            if (_lastCpuTotal.HasValue && _lastCpuIdle.HasValue)
            {
                var totalDelta = total - _lastCpuTotal.Value;
                var idleDelta = idleAll - _lastCpuIdle.Value;
                if (totalDelta > 0)
                {
                    return (double)(totalDelta - idleDelta) / totalDelta * 100.0;
                }
            }

            _lastCpuTotal = total;
            _lastCpuIdle = idleAll;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read /proc/stat for CPU usage");
        }

        return null;
    }

    private double? TryGetMemoryUsage()
    {
        if (_memoryCounter != null)
        {
            var availableMemoryMB = _memoryCounter.NextValue();
            var totalAvailableBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            if (totalAvailableBytes > 0)
            {
                var totalMemoryMB = totalAvailableBytes / (1024.0 * 1024.0);
                var usedPercent = ((totalMemoryMB - availableMemoryMB) / totalMemoryMB) * 100.0;
                return Math.Clamp(usedPercent, 0.0, 100.0);
            }
        }

        if (OperatingSystem.IsLinux())
            return TryGetLinuxMemoryUsage();

        return null;
    }

    private double? TryGetLinuxMemoryUsage()
    {
        try
        {
            if (!File.Exists("/proc/meminfo"))
                return null;

            long? totalKb = null;
            long? availableKb = null;

            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:", StringComparison.OrdinalIgnoreCase))
                    totalKb = ParseMemInfoValue(line);
                if (line.StartsWith("MemAvailable:", StringComparison.OrdinalIgnoreCase))
                    availableKb = ParseMemInfoValue(line);

                if (totalKb.HasValue && availableKb.HasValue)
                    break;
            }

            if (!totalKb.HasValue || !availableKb.HasValue || totalKb.Value <= 0)
                return null;

            var usedPercent = (double)(totalKb.Value - availableKb.Value) / totalKb.Value * 100.0;
            return Math.Clamp(usedPercent, 0.0, 100.0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read /proc/meminfo for memory usage");
            return null;
        }
    }

    private static long? ParseMemInfoValue(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return null;

        if (long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return value;

        return null;
    }

    private double? TryGetDiskUsage()
    {
        try
        {
            var root = Path.GetPathRoot(AppContext.BaseDirectory);
            if (string.IsNullOrWhiteSpace(root))
                return null;

            var drive = new DriveInfo(root);
            if (!drive.IsReady || drive.TotalSize <= 0)
                return null;

            return (double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100.0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read disk usage");
            return null;
        }
    }

    private async Task<bool> TestCosmosDbConnectivity()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var cosmosClient = scope.ServiceProvider.GetRequiredService<Microsoft.Azure.Cosmos.CosmosClient>();
            
            var database = cosmosClient.GetDatabase("mindex");
            await database.ReadAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private Task<bool> TestEventGridConnectivity()
    {
        var endpoint = _configuration["EventGrid:TopicEndpoint"];
        var key = _configuration["EventGrid:AccessKey"];
        var hasConfig = !string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(key);
        var validUri = hasConfig && Uri.TryCreate(endpoint, UriKind.Absolute, out _);
        return Task.FromResult(hasConfig && validUri);
    }

    private async Task<bool> TestServiceBusConnectivity()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<ServiceBusClient>();
            var receiver = client.CreateReceiver("mycorrhizae-events");
            await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2));
            await receiver.CloseAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task SendAlert(SystemAlert alert)
    {
        try
        {
            // Broadcast alert to all connected clients
            await _hubContext.Clients.Group("AllUsers").SendAsync("SystemAlert", alert);
            
            // Log the alert
            _logger.LogWarning("System alert: {Category} - {Message}", alert.Category, alert.Message);
            
            // Here you could also send alerts to external systems like:
            // - Email notifications
            // - Slack/Teams messages
            // - SMS alerts for critical issues
            // - Ticketing systems
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send alert");
        }
    }

    public override void Dispose()
    {
        _cpuCounter?.Dispose();
        _memoryCounter?.Dispose();
        base.Dispose();
    }
}

// Supporting classes
public class SystemMetrics
{
    public DateTime Timestamp { get; set; }
    public double? CpuUsage { get; set; }
    public double? MemoryUsage { get; set; }
    public double? DiskUsage { get; set; }
}

public class SystemAlert
{
    public string? Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // Info, Warning, Critical
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? DeviceId { get; set; }
    public string[] Recommendations { get; set; } = Array.Empty<string>();
} 