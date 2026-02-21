namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service interface for data analytics, reports, and insights
/// </summary>
public interface IAnalyticsService
{
    /// <summary>
    /// Get time-series data for a metric
    /// </summary>
    Task<TimeSeriesResult> GetTimeSeriesAsync(string metric, DateTime start, DateTime end, string? deviceId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get biodiversity metrics
    /// </summary>
    Task<BiodiversityMetrics> GetBiodiversityMetricsAsync(DateTime? start = null, DateTime? end = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get analytics report summary
    /// </summary>
    Task<AnalyticsReport> GetReportAsync(string reportType, DateTime? start = null, DateTime? end = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Time-series data result
/// </summary>
public class TimeSeriesResult
{
    public string Metric { get; set; } = string.Empty;
    public List<TimeSeriesPoint> DataPoints { get; set; } = new();
}

/// <summary>
/// Single time-series data point
/// </summary>
public class TimeSeriesPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
}

/// <summary>
/// Biodiversity metrics
/// </summary>
public class BiodiversityMetrics
{
    public int SpeciesCount { get; set; }
    public int ObservationCount { get; set; }
    public Dictionary<string, int> SpeciesByPhylum { get; set; } = new();
    /// <summary>Shannon diversity index (from MATLAB when available)</summary>
    public double? ShannonIndex { get; set; }
    /// <summary>Simpson diversity index (from MATLAB when available)</summary>
    public double? SimpsonIndex { get; set; }
    /// <summary>Chao1 species richness estimator (from MATLAB when available)</summary>
    public double? ChaoEstimator { get; set; }
    /// <summary>Rarefaction curve data points (from MATLAB when available)</summary>
    public double[]? RarefactionCurve { get; set; }
}

/// <summary>
/// Analytics report
/// </summary>
public class AnalyticsReport
{
    public string ReportType { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public Dictionary<string, object> Summary { get; set; } = new();
}
