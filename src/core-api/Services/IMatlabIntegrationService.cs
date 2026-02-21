namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service interface for MATLAB-driven analytics, AI, and visualization.
/// Integrates with MATLAB Engine API for .NET or MATLAB Production Server.
/// </summary>
public interface IMatlabIntegrationService
{
    /// <summary>
    /// Execute a named MATLAB analysis function with arguments.
    /// </summary>
    Task<MatlabResult> ExecuteAnalysisAsync(string functionName, object[] args, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a visualization (plot) and return as image bytes.
    /// </summary>
    Task<byte[]> GenerateVisualizationAsync(string plotType, object data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Run anomaly detection on time-series telemetry data.
    /// </summary>
    Task<AnomalyDetectionResult> DetectAnomaliesAsync(double[] timeSeries, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forecast environmental metric (temperature, humidity, etc.) for given horizon.
    /// </summary>
    Task<ForecastResult> ForecastEnvironmentalAsync(string metric, int horizonHours, double[]? historicalData = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Classify fungal morphology from signal vector.
    /// </summary>
    Task<ClassificationResult> ClassifyFungalMorphologyAsync(double[] signalVector, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if MATLAB integration is available and healthy.
    /// </summary>
    Task<MatlabHealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Generic result from MATLAB execution.
/// </summary>
public class MatlabResult
{
    public bool Success { get; set; }
    public Dictionary<string, object> Outputs { get; set; } = new();
    public string? Error { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public T? Get<T>(string key) where T : class => Outputs.TryGetValue(key, out var v) ? v as T : default;
    public T GetValue<T>(string key) where T : struct => Outputs.TryGetValue(key, out var v) && v is T t ? t : default;
}

/// <summary>
/// Anomaly detection result.
/// </summary>
public class AnomalyDetectionResult
{
    public bool[] IsAnomaly { get; set; } = Array.Empty<bool>();
    public double[] AnomalyScores { get; set; } = Array.Empty<double>();
    public int AnomalyCount { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Environmental forecast result.
/// </summary>
public class ForecastResult
{
    public double[] Predictions { get; set; } = Array.Empty<double>();
    public DateTime[] Timestamps { get; set; } = Array.Empty<DateTime>();
    public double ConfidenceInterval { get; set; }
}

/// <summary>
/// Fungal morphology classification result.
/// </summary>
public class ClassificationResult
{
    public string? TopSpecies { get; set; }
    public double Confidence { get; set; }
    public List<SpeciesCandidate> Alternatives { get; set; } = new();
}

/// <summary>
/// Alternative species with confidence score.
/// </summary>
public class SpeciesCandidate
{
    public string Species { get; set; } = string.Empty;
    public double Confidence { get; set; }
}

/// <summary>
/// MATLAB integration health status.
/// </summary>
public class MatlabHealthStatus
{
    public bool Available { get; set; }
    public string Mode { get; set; } = "Unavailable";
    public string? Message { get; set; }
}
