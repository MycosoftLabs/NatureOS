using System.Net.Http.Json;
using System.Text.Json;

namespace NatureOS.CoreApi.Services;

/// <summary>
/// MATLAB integration service supporting Engine API and Production Server modes.
/// When MATLAB is not available, provides fallback implementations for core analytics.
/// </summary>
public class MatlabIntegrationService : IMatlabIntegrationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MatlabIntegrationService> _logger;
    private readonly string _integrationMode;
    private readonly string? _productionServerUrl;

    public MatlabIntegrationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<MatlabIntegrationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _integrationMode = configuration["Matlab:IntegrationMode"] ?? "Fallback";
        _productionServerUrl = configuration["Matlab:ProductionServerUrl"];
    }

    public async Task<MatlabResult> ExecuteAnalysisAsync(string functionName, object[] args, CancellationToken cancellationToken = default)
    {
        if (_integrationMode == "ProductionServer" && !string.IsNullOrEmpty(_productionServerUrl))
        {
            return await ExecuteViaProductionServerAsync(functionName, args, cancellationToken);
        }

        // Fallback: use built-in C# implementations for known functions
        return await ExecuteFallbackAsync(functionName, args, cancellationToken);
    }

    public async Task<byte[]> GenerateVisualizationAsync(string plotType, object data, CancellationToken cancellationToken = default)
    {
        if (_integrationMode == "ProductionServer" && !string.IsNullOrEmpty(_productionServerUrl))
        {
            return await GenerateViaProductionServerAsync(plotType, data, cancellationToken);
        }

        // Fallback: return empty PNG placeholder (1x1 transparent)
        return Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
    }

    public async Task<AnomalyDetectionResult> DetectAnomaliesAsync(double[] timeSeries, CancellationToken cancellationToken = default)
    {
        if (_integrationMode == "ProductionServer" && !string.IsNullOrEmpty(_productionServerUrl))
        {
            return await DetectAnomaliesViaProductionServerAsync(timeSeries, cancellationToken);
        }

        // Fallback: simple statistical anomaly detection (z-score based)
        return await Task.FromResult(DetectAnomaliesFallback(timeSeries));
    }

    public async Task<ForecastResult> ForecastEnvironmentalAsync(string metric, int horizonHours, double[]? historicalData = null, CancellationToken cancellationToken = default)
    {
        if (_integrationMode == "ProductionServer" && !string.IsNullOrEmpty(_productionServerUrl))
        {
            return await ForecastViaProductionServerAsync(metric, horizonHours, historicalData, cancellationToken);
        }

        // Fallback: simple linear extrapolation
        return await Task.FromResult(ForecastFallback(metric, horizonHours, historicalData));
    }

    public async Task<ClassificationResult> ClassifyFungalMorphologyAsync(double[] signalVector, CancellationToken cancellationToken = default)
    {
        if (_integrationMode == "ProductionServer" && !string.IsNullOrEmpty(_productionServerUrl))
        {
            return await ClassifyViaProductionServerAsync(signalVector, cancellationToken);
        }

        // Fallback: return unknown with low confidence
        return await Task.FromResult(new ClassificationResult
        {
            TopSpecies = "Unknown",
            Confidence = 0,
            Alternatives = new List<SpeciesCandidate>()
        });
    }

    public async Task<MatlabHealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken = default)
    {
        if (_integrationMode == "ProductionServer" && !string.IsNullOrEmpty(_productionServerUrl))
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync($"{_productionServerUrl.TrimEnd('/')}/health", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return new MatlabHealthStatus
                    {
                        Available = true,
                        Mode = "ProductionServer",
                        Message = "MATLAB Production Server is operational"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MATLAB Production Server health check failed");
            }
        }

        return await Task.FromResult(new MatlabHealthStatus
        {
            Available = _integrationMode == "Fallback",
            Mode = _integrationMode,
            Message = _integrationMode == "Fallback"
                ? "Using fallback C# implementations (MATLAB not configured)"
                : "MATLAB Engine/Production Server not available"
        });
    }

    private async Task<MatlabResult> ExecuteViaProductionServerAsync(string functionName, object[] args, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var payload = new { functionName, args };
            var response = await client.PostAsJsonAsync($"{_productionServerUrl!.TrimEnd('/')}/analysis/{functionName}", payload, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<MatlabResult>(cancellationToken);
            return result ?? new MatlabResult { Success = false, Error = "Empty response" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MATLAB Production Server analysis failed for {Function}", functionName);
            return new MatlabResult { Success = false, Error = ex.Message };
        }
    }

    private async Task<MatlabResult> ExecuteFallbackAsync(string functionName, object[] args, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        if (functionName == "calculateBiodiversityIndices" && args.Length > 0)
        {
            var arg0 = args[0];
            string[] speciesIds = arg0 switch
            {
                string[] sa => sa.Where(s => !string.IsNullOrEmpty(s)).ToArray(),
                object[] oa => oa.Select(x => x?.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToArray(),
                _ => Array.Empty<string>()
            };
            var counts = speciesIds.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
            var n = counts.Values.Sum();
            if (n == 0) return new MatlabResult { Success = true, Outputs = new Dictionary<string, object> { ["shannon"] = 0.0, ["simpson"] = 0.0, ["chao1"] = 0.0, ["rarefaction"] = Array.Empty<double>() } };
            double shannon = 0, simpson = 0;
            foreach (var c in counts.Values.Where(c => c > 0))
            {
                var p = (double)c / n;
                shannon -= p * Math.Log(p);
                simpson += p * p;
            }
            return new MatlabResult
            {
                Success = true,
                Outputs = new Dictionary<string, object>
                {
                    ["shannon"] = shannon,
                    ["simpson"] = 1 - simpson,
                    ["chao1"] = counts.Count + (counts.Values.Count(c => c == 1) * (counts.Values.Count(c => c == 1) - 1)) / (2.0 * Math.Max(1, counts.Values.Count(c => c == 2))),
                    ["rarefaction"] = Enumerable.Range(1, Math.Min(100, n)).Select(i => (double)counts.Count).ToArray()
                }
            };
        }

        return new MatlabResult { Success = false, Error = $"Unknown function: {functionName}" };
    }

    private async Task<byte[]> GenerateViaProductionServerAsync(string plotType, object data, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var payload = new { plotType, data };
            var response = await client.PostAsJsonAsync($"{_productionServerUrl!.TrimEnd('/')}/visualization", payload, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MATLAB visualization failed for {PlotType}", plotType);
            return Array.Empty<byte>();
        }
    }

    private async Task<AnomalyDetectionResult> DetectAnomaliesViaProductionServerAsync(double[] timeSeries, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsJsonAsync($"{_productionServerUrl!.TrimEnd('/')}/anomaly", timeSeries, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AnomalyDetectionResult>(cancellationToken);
            return result ?? DetectAnomaliesFallback(timeSeries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MATLAB anomaly detection failed");
            return DetectAnomaliesFallback(timeSeries);
        }
    }

    private static AnomalyDetectionResult DetectAnomaliesFallback(double[] timeSeries)
    {
        if (timeSeries == null || timeSeries.Length == 0)
            return new AnomalyDetectionResult { Message = "No data" };

        var mean = timeSeries.Average();
        var variance = timeSeries.Select(x => (x - mean) * (x - mean)).Average();
        var std = Math.Sqrt(variance);
        if (std < 1e-10) std = 1;

        var scores = timeSeries.Select(x => Math.Abs((x - mean) / std)).ToArray();
        var isAnomaly = scores.Select(s => s > 3).ToArray();
        return new AnomalyDetectionResult
        {
            IsAnomaly = isAnomaly,
            AnomalyScores = scores,
            AnomalyCount = isAnomaly.Count(x => x),
            Message = "Fallback z-score detection"
        };
    }

    private async Task<ForecastResult> ForecastViaProductionServerAsync(string metric, int horizonHours, double[]? historicalData, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var payload = new { metric, horizonHours, historicalData = historicalData ?? Array.Empty<double>() };
            var response = await client.PostAsJsonAsync($"{_productionServerUrl!.TrimEnd('/')}/forecast", payload, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ForecastResult>(cancellationToken);
            return result ?? ForecastFallback(metric, horizonHours, historicalData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MATLAB forecast failed");
            return ForecastFallback(metric, horizonHours, historicalData);
        }
    }

    private static ForecastResult ForecastFallback(string metric, int horizonHours, double[]? historicalData)
    {
        var data = historicalData ?? Array.Empty<double>();
        var lastValue = data.Length > 0 ? data[^1] : 0;
        var slope = data.Length >= 2 ? (data[^1] - data[^2]) : 0;
        var predictions = Enumerable.Range(1, horizonHours).Select(i => lastValue + slope * i).ToArray();
        var baseTime = DateTime.UtcNow;
        var timestamps = Enumerable.Range(1, horizonHours).Select(i => baseTime.AddHours(i)).ToArray();
        return new ForecastResult
        {
            Predictions = predictions,
            Timestamps = timestamps,
            ConfidenceInterval = 0.95
        };
    }

    private async Task<ClassificationResult> ClassifyViaProductionServerAsync(double[] signalVector, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsJsonAsync($"{_productionServerUrl!.TrimEnd('/')}/classify", signalVector, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ClassificationResult>(cancellationToken);
            return result ?? new ClassificationResult { TopSpecies = "Unknown", Confidence = 0 };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MATLAB classification failed");
            return new ClassificationResult { TopSpecies = "Unknown", Confidence = 0 };
        }
    }
}
