namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service interface for threshold alerts and anomaly detection
/// </summary>
public interface IAlertService
{
    /// <summary>
    /// Record a new alert
    /// </summary>
    Task RecordAlertAsync(SystemAlert alert, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get alerts with optional filters
    /// </summary>
    Task<IEnumerable<SystemAlert>> GetAlertsAsync(AlertQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledge an alert
    /// </summary>
    Task AcknowledgeAlertAsync(string alertId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get alert statistics
    /// </summary>
    Task<AlertStatistics> GetStatisticsAsync(DateTime? since = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Alert query parameters
/// </summary>
public class AlertQuery
{
    public string? Type { get; set; }
    public string? Severity { get; set; }
    public string? DeviceId { get; set; }
    public DateTime? Since { get; set; }
    public int Limit { get; set; } = 100;
}

/// <summary>
/// Alert statistics
/// </summary>
public class AlertStatistics
{
    public int TotalAlerts { get; set; }
    public int CriticalCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
    public Dictionary<string, int> ByType { get; set; } = new();
}
