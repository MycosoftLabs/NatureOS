namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service for data analytics, reports, and insights
/// </summary>
public class AnalyticsService : IAnalyticsService
{
    private readonly IEventService _eventService;
    private readonly IMatlabIntegrationService _matlabService;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(IEventService eventService, ILogger<AnalyticsService> logger, IMatlabIntegrationService matlabService)
    {
        _eventService = eventService;
        _logger = logger;
        _matlabService = matlabService;
    }

    public async Task<TimeSeriesResult> GetTimeSeriesAsync(string metric, DateTime start, DateTime end, string? deviceId = null, CancellationToken cancellationToken = default)
    {
        var query = new EventQuery
        {
            StartTime = start,
            EndTime = end,
            SourceDevice = deviceId,
            PageSize = 1000
        };
        var events = await _eventService.GetEventsAsync(query, cancellationToken);
        var points = events.Items
            .Where(e => e.Timestamp >= start && e.Timestamp <= end)
            .GroupBy(e => new DateTime(e.Timestamp.Year, e.Timestamp.Month, e.Timestamp.Day, e.Timestamp.Hour, 0, 0, DateTimeKind.Utc))
            .Select(g => new TimeSeriesPoint { Timestamp = g.Key, Value = g.Count() })
            .OrderBy(p => p.Timestamp)
            .ToList();

        return new TimeSeriesResult { Metric = metric, DataPoints = points };
    }

    public async Task<AnalyticsBiodiversityMetrics> GetBiodiversityMetricsAsync(DateTime? start = null, DateTime? end = null, CancellationToken cancellationToken = default)
    {
        var query = new EventQuery
        {
            StartTime = start ?? DateTime.UtcNow.AddDays(-30),
            EndTime = end ?? DateTime.UtcNow,
            PageSize = 10000
        };
        var events = await _eventService.GetEventsAsync(query, cancellationToken);
        var items = events.Items.ToList();
        var byPhylum = items
            .Where(e => !string.IsNullOrEmpty(e.References?.Taxonomy?.Phylum))
            .GroupBy(e => e.References!.Taxonomy!.Phylum!)
            .ToDictionary(g => g.Key, g => g.Count());

        var speciesIds = items
            .Select(e => e.References?.Taxonomy?.Species ?? e.References?.Taxonomy?.ScientificName ?? "unknown")
            .Where(s => !string.IsNullOrEmpty(s))
            .ToArray();

        var metrics = new AnalyticsBiodiversityMetrics
        {
            SpeciesCount = speciesIds.Distinct().Count(),
            ObservationCount = items.Count,
            SpeciesByPhylum = byPhylum
        };

        // Enrich with MATLAB biodiversity indices when available
        if (speciesIds.Length > 0)
        {
            try
            {
                var matlabResult = await _matlabService.ExecuteAnalysisAsync(
                    "calculateBiodiversityIndices",
                    new object[] { speciesIds },
                    cancellationToken);
                if (matlabResult.Success)
                {
                    metrics.ShannonIndex = matlabResult.GetValue<double>("shannon");
                    metrics.SimpsonIndex = matlabResult.GetValue<double>("simpson");
                    metrics.ChaoEstimator = matlabResult.GetValue<double>("chao1");
                    var raref = matlabResult.Get<double[]>("rarefaction");
                    if (raref != null) metrics.RarefactionCurve = raref;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "MATLAB biodiversity indices not available, using basic metrics only");
            }
        }

        return metrics;
    }

    public async Task<AnalyticsReport> GetReportAsync(string reportType, DateTime? start = null, DateTime? end = null, CancellationToken cancellationToken = default)
    {
        var metrics = await GetBiodiversityMetricsAsync(start, end, cancellationToken);
        return new AnalyticsReport
        {
            ReportType = reportType,
            GeneratedAt = DateTime.UtcNow,
            Summary = new Dictionary<string, object>
            {
                ["speciesCount"] = metrics.SpeciesCount,
                ["observationCount"] = metrics.ObservationCount,
                ["phylaCount"] = metrics.SpeciesByPhylum.Count
            }
        };
    }
}
