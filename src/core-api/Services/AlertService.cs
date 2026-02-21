namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service for threshold alerts and anomaly detection
/// </summary>
public class AlertService : IAlertService
{
    private readonly ILogger<AlertService> _logger;
    private readonly List<SystemAlert> _alerts = new();
    private static readonly object _lock = new();

    public AlertService(ILogger<AlertService> logger)
    {
        _logger = logger;
    }

    public Task RecordAlertAsync(SystemAlert alert, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(alert.Id))
            alert.Id = Guid.NewGuid().ToString("N")[..12];
        lock (_lock)
        {
            _alerts.Insert(0, alert);
            if (_alerts.Count > 1000)
                _alerts.RemoveRange(1000, _alerts.Count - 1000);
        }
        _logger.LogWarning("Alert recorded: {Type} {Severity} - {Message}", alert.Type, alert.Severity, alert.Message);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<SystemAlert>> GetAlertsAsync(AlertQuery? query = null, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var q = _alerts.AsEnumerable();
            if (query != null)
            {
                if (!string.IsNullOrEmpty(query.Type))
                    q = q.Where(a => string.Equals(a.Type, query.Type, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(query.Severity))
                    q = q.Where(a => string.Equals(a.Severity, query.Severity, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(query.DeviceId))
                    q = q.Where(a => string.Equals(a.DeviceId, query.DeviceId, StringComparison.OrdinalIgnoreCase));
                if (query.Since.HasValue)
                    q = q.Where(a => a.Timestamp >= query.Since.Value);
                q = q.Take(query.Limit);
            }
            return Task.FromResult(q);
        }
    }

    public Task AcknowledgeAlertAsync(string alertId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Alert {AlertId} acknowledged", alertId);
        return Task.CompletedTask;
    }

    public Task<AlertStatistics> GetStatisticsAsync(DateTime? since = null, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var q = _alerts.AsEnumerable();
            if (since.HasValue)
                q = q.Where(a => a.Timestamp >= since.Value);
            var list = q.ToList();
            return Task.FromResult(new AlertStatistics
            {
                TotalAlerts = list.Count,
                CriticalCount = list.Count(a => string.Equals(a.Severity, "Critical", StringComparison.OrdinalIgnoreCase)),
                WarningCount = list.Count(a => string.Equals(a.Severity, "Warning", StringComparison.OrdinalIgnoreCase)),
                InfoCount = list.Count(a => string.Equals(a.Severity, "Info", StringComparison.OrdinalIgnoreCase)),
                ByType = list.GroupBy(a => a.Type).ToDictionary(g => g.Key, g => g.Count())
            });
        }
    }
}
