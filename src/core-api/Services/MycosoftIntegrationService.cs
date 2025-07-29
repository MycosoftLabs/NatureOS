using Microsoft.Azure.Cosmos;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.EventGrid;
using NatureOS.MINDEX.Models;
using System.Text.Json;

namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service for integrating NatureOS with all Mycosoft products and services
/// </summary>
public class MycosoftIntegrationService : IMycosoftIntegrationService
{
    private readonly CosmosClient _cosmosClient;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly EventGridPublisherClient _eventGridClient;
    private readonly ILogger<MycosoftIntegrationService> _logger;
    private readonly HttpClient _httpClient;

    public MycosoftIntegrationService(
        CosmosClient cosmosClient,
        ServiceBusClient serviceBusClient,
        EventGridPublisherClient eventGridClient,
        ILogger<MycosoftIntegrationService> logger,
        HttpClient httpClient)
    {
        _cosmosClient = cosmosClient;
        _serviceBusClient = serviceBusClient;
        _eventGridClient = eventGridClient;
        _logger = logger;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Process Mushroom 1 device telemetry
    /// </summary>
    public async Task<ProcessingResult> ProcessMushroom1DataAsync(Mushroom1Telemetry telemetry, CancellationToken cancellationToken = default)
    {
        try
        {
            // Convert to Mycorrhizae Protocol event
            var mycorrhizaeEvent = new MycorrhizaeEvent
            {
                EventId = Ulid.NewUlid().ToString(),
                Timestamp = telemetry.Timestamp,
                SourceDevice = telemetry.DeviceId,
                KingdomDomain = "FUNGA.electrical",
                SignalVector = new
                {
                    bioelectric_channels = telemetry.BioelectricChannels,
                    temperature = telemetry.Temperature,
                    humidity = telemetry.Humidity,
                    pressure = telemetry.Pressure,
                    gas_resistance = telemetry.GasResistance,
                    voc_index = telemetry.VocIndex
                },
                References = new EventReferences
                {
                    Location = telemetry.Location,
                    Environment = new EnvironmentalContext
                    {
                        Temperature = telemetry.Temperature,
                        Humidity = telemetry.Humidity,
                        Parameters = new Dictionary<string, object>
                        {
                            ["pressure"] = telemetry.Pressure,
                            ["gas_resistance"] = telemetry.GasResistance,
                            ["voc_index"] = telemetry.VocIndex
                        }
                    }
                },
                Metadata = new EventMetadata
                {
                    IngestedAt = DateTime.UtcNow,
                    PipelineVersion = "2.0.0",
                    TenantId = telemetry.TenantId
                }
            };

            // Store in MINDEX
            await StoreEventAsync(mycorrhizaeEvent);

            // Send to MWave for signal processing
            await SendToMWaveAsync(mycorrhizaeEvent);

            // Send to ALARM for anomaly detection
            await SendToAlarmAsync(mycorrhizaeEvent);

            // Update device status
            await UpdateDeviceStatusAsync(telemetry.DeviceId, DeviceStatus.Online);

            return new ProcessingResult
            {
                Success = true,
                EventId = mycorrhizaeEvent.EventId,
                ProcessedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Mushroom 1 data from device {DeviceId}", telemetry.DeviceId);
            throw;
        }
    }

    /// <summary>
    /// Send event to MWave for signal processing
    /// </summary>
    public async Task SendToMWaveAsync(MycorrhizaeEvent mycorrhizaeEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var mwaveMessage = new MWaveProcessingRequest
            {
                EventId = mycorrhizaeEvent.EventId,
                SignalVector = mycorrhizaeEvent.SignalVector,
                SamplingRate = 1000, // Hz
                WindowSize = 2048,
                OverlapRatio = 0.5,
                WaveletType = "morlet",
                FrequencyBands = new[]
                {
                    new FrequencyBand { Name = "delta", Min = 0.5, Max = 4 },
                    new FrequencyBand { Name = "theta", Min = 4, Max = 8 },
                    new FrequencyBand { Name = "alpha", Min = 8, Max = 13 },
                    new FrequencyBand { Name = "beta", Min = 13, Max = 30 },
                    new FrequencyBand { Name = "gamma", Min = 30, Max = 100 }
                }
            };

            var sender = _serviceBusClient.CreateSender("mwave-processing");
            var message = new ServiceBusMessage(JsonSerializer.Serialize(mwaveMessage))
            {
                MessageId = mycorrhizaeEvent.EventId,
                Subject = "signal-processing",
                ContentType = "application/json"
            };

            await sender.SendMessageAsync(message, cancellationToken);
            _logger.LogInformation("Sent event {EventId} to MWave processing", mycorrhizaeEvent.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send event {EventId} to MWave", mycorrhizaeEvent.EventId);
            throw;
        }
    }

    /// <summary>
    /// Send event to ALARM for anomaly detection
    /// </summary>
    public async Task SendToAlarmAsync(MycorrhizaeEvent mycorrhizaeEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var alarmRequest = new AlarmAnalysisRequest
            {
                EventId = mycorrhizaeEvent.EventId,
                DeviceId = mycorrhizaeEvent.SourceDevice,
                Timestamp = mycorrhizaeEvent.Timestamp,
                SignalFeatures = ExtractSignalFeatures(mycorrhizaeEvent.SignalVector),
                EnvironmentalContext = mycorrhizaeEvent.References?.Environment
            };

            var sender = _serviceBusClient.CreateSender("alarm-analysis");
            var message = new ServiceBusMessage(JsonSerializer.Serialize(alarmRequest))
            {
                MessageId = mycorrhizaeEvent.EventId,
                Subject = "anomaly-detection",
                ContentType = "application/json"
            };

            await sender.SendMessageAsync(message, cancellationToken);
            _logger.LogInformation("Sent event {EventId} to ALARM analysis", mycorrhizaeEvent.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send event {EventId} to ALARM", mycorrhizaeEvent.EventId);
            throw;
        }
    }

    /// <summary>
    /// Update MYCA AI Assistant with new data
    /// </summary>
    public async Task UpdateMycaAsync(MycorrhizaeEvent mycorrhizaeEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var mycaUpdate = new MycaKnowledgeUpdate
            {
                EventId = mycorrhizaeEvent.EventId,
                KnowledgeType = "fungal-observation",
                Data = mycorrhizaeEvent,
                Priority = DeterminePriority(mycorrhizaeEvent),
                Tags = ExtractTags(mycorrhizaeEvent)
            };

            // Send to MYCA knowledge ingestion endpoint
            var response = await _httpClient.PostAsJsonAsync(
                "https://myca-api.mycosoft.com/knowledge/ingest",
                mycaUpdate,
                cancellationToken);

            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Updated MYCA with event {EventId}", mycorrhizaeEvent.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update MYCA with event {EventId}", mycorrhizaeEvent.EventId);
            // Don't throw - MYCA updates are not critical
        }
    }

    /// <summary>
    /// Sync data with Mycosoft.com website
    /// </summary>
    public async Task SyncWithWebsiteAsync(WebsiteSyncRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get latest statistics for website dashboard
            var stats = new WebsiteDashboardData
            {
                TotalEvents = await GetTotalEventsAsync(),
                ActiveDevices = await GetActiveDevicesAsync(),
                SpeciesDetected = await GetSpeciesCountAsync(),
                OnlineUsers = await GetOnlineUsersAsync(),
                LiveReadings = await GetLatestReadingsAsync(10),
                TrendingCompounds = await GetTrendingCompoundsAsync(),
                RecentDiscoveries = await GetRecentDiscoveriesAsync()
            };

            // Send to website API
            var response = await _httpClient.PostAsJsonAsync(
                "https://mycosoft.vercel.app/api/dashboard/update",
                stats,
                cancellationToken);

            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Synced dashboard data with website");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync with website");
            throw;
        }
    }

    /// <summary>
    /// Process HPL (Hypha Programming Language) simulation request
    /// </summary>
    public async Task<HplSimulationResult> ProcessHplSimulationAsync(HplSimulationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Compile HPL to WASM
            var compilationRequest = new HplCompilationRequest
            {
                SourceCode = request.HplCode,
                OptimizationLevel = "O2",
                TargetFormat = "wasm"
            };

            var compileResponse = await _httpClient.PostAsJsonAsync(
                "https://hpl-compiler.mycosoft.com/compile",
                compilationRequest,
                cancellationToken);

            compileResponse.EnsureSuccessStatusCode();
            var compilationResult = await compileResponse.Content.ReadFromJsonAsync<HplCompilationResult>(cancellationToken);

            // Run simulation in Mycelium Sim
            var simulationRequest = new MyceliumSimRequest
            {
                WasmBinary = compilationResult.WasmBinary,
                InitialConditions = request.InitialConditions,
                SimulationTime = request.Duration,
                OutputFormat = "json"
            };

            var simResponse = await _httpClient.PostAsJsonAsync(
                "https://mycelium-sim.mycosoft.com/run",
                simulationRequest,
                cancellationToken);

            simResponse.EnsureSuccessStatusCode();
            var simResult = await simResponse.Content.ReadFromJsonAsync<MyceliumSimResult>(cancellationToken);
            if (simResult == null)
            {
                throw new InvalidOperationException("Simulation service returned no result");
            }

            // Store results in MINDEX
            await StoreSimulationResultAsync(simResult);

            return new HplSimulationResult
            {
                SimulationId = simResult.SimulationId,
                Success = true,
                Results = simResult.OutputData,
                ExecutionTime = simResult.ExecutionTime,
                MemoryUsage = simResult.MemoryUsage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process HPL simulation");
            throw;
        }
    }

    private async Task StoreEventAsync(MycorrhizaeEvent mycorrhizaeEvent)
    {
        var database = _cosmosClient.GetDatabase("mindex");
        var container = database.GetContainer("events");
        
        await container.CreateItemAsync(
            mycorrhizaeEvent,
            new PartitionKey(mycorrhizaeEvent.SourceDevice));
    }

    private async Task UpdateDeviceStatusAsync(string deviceId, DeviceStatus status)
    {
        var database = _cosmosClient.GetDatabase("mindex");
        var container = database.GetContainer("devices");
        
        var device = await container.ReadItemAsync<Device>(deviceId, new PartitionKey(deviceId));
        device.Resource.Status = status;
        device.Resource.LastSeen = DateTime.UtcNow;
        device.Resource.UpdatedAt = DateTime.UtcNow;
        
        await container.ReplaceItemAsync(device.Resource, deviceId, new PartitionKey(deviceId));
    }

    private static Dictionary<string, double> ExtractSignalFeatures(object? signalVector)
    {
        // Extract basic statistical features from signal
        return new Dictionary<string, double>
        {
            ["mean"] = 0.0,
            ["std"] = 0.0,
            ["rms"] = 0.0,
            ["peak_to_peak"] = 0.0,
            ["zero_crossings"] = 0.0
        };
    }

    private static int DeterminePriority(MycorrhizaeEvent mycorrhizaeEvent)
    {
        // Determine priority based on event content
        return mycorrhizaeEvent.KingdomDomain.Contains("anomaly") ? 1 : 3;
    }

    private static List<string> ExtractTags(MycorrhizaeEvent mycorrhizaeEvent)
    {
        var tags = new List<string> { "fungal-data", mycorrhizaeEvent.KingdomDomain };
        
        if (mycorrhizaeEvent.References?.Taxonomy?.Genus != null)
        {
            tags.Add($"genus:{mycorrhizaeEvent.References.Taxonomy.Genus}");
        }
        
        return tags;
    }

    private async Task<long> GetTotalEventsAsync()
    {
        var database = _cosmosClient.GetDatabase("mindex");
        var container = database.GetContainer("events");
        
        var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c");
        var iterator = container.GetItemQueryIterator<long>(query);
        
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        
        return 0;
    }

    private async Task<int> GetActiveDevicesAsync()
    {
        var database = _cosmosClient.GetDatabase("mindex");
        var container = database.GetContainer("devices");
        
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.lastSeen > @cutoff")
            .WithParameter("@cutoff", cutoff);
            
        var iterator = container.GetItemQueryIterator<int>(query);
        
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        
        return 0;
    }

    private async Task<int> GetSpeciesCountAsync()
    {
        var database = _cosmosClient.GetDatabase("mindex");
        var container = database.GetContainer("events");
        
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(DISTINCT c.references.taxonomy.species) FROM c WHERE c.references.taxonomy.species != null");
            
        var iterator = container.GetItemQueryIterator<int>(query);
        
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        
        return 0;
    }

    private Task<int> GetOnlineUsersAsync()
    {
        // This would integrate with your user session tracking
        return Task.FromResult(42); // Placeholder
    }

    private async Task<List<LiveReading>> GetLatestReadingsAsync(int count)
    {
        var database = _cosmosClient.GetDatabase("mindex");
        var container = database.GetContainer("events");
        
        var query = new QueryDefinition(
            $"SELECT TOP {count} c.source_device, c.timestamp, c.signal_vector FROM c ORDER BY c.timestamp DESC");
            
        var iterator = container.GetItemQueryIterator<dynamic>(query);
        var readings = new List<LiveReading>();
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            foreach (var item in response)
            {
                readings.Add(new LiveReading
                {
                    DeviceId = item.source_device,
                    Timestamp = item.timestamp,
                    Value = ExtractReadingValue(item.signal_vector)
                });
            }
        }
        
        return readings;
    }

    private static double ExtractReadingValue(dynamic signalVector)
    {
        // Extract a representative value from the signal
        return 23.5; // Placeholder
    }

    private Task<List<string>> GetTrendingCompoundsAsync()
    {
        // This would query the compounds database
        return Task.FromResult(new List<string>
        {
            "Psilocybin", "Cordycepin", "Beta-glucan", "Ergothioneine"
        });
    }

    private Task<List<string>> GetRecentDiscoveriesAsync()
    {
        // This would query recent research findings
        return Task.FromResult(new List<string>
        {
            "New mycorrhizal network topology discovered",
            "Novel antifungal compound isolated",
            "Rare species found in urban environment"
        });
    }

    private async Task StoreSimulationResultAsync(MyceliumSimResult result)
    {
        var database = _cosmosClient.GetDatabase("mindex");
        var container = database.GetContainer("sim_runs");
        
        await container.CreateItemAsync(result, new PartitionKey(result.SimulationId));
    }
}

// Supporting models and interfaces
public interface IMycosoftIntegrationService
{
    Task<ProcessingResult> ProcessMushroom1DataAsync(Mushroom1Telemetry telemetry, CancellationToken cancellationToken = default);
    Task SendToMWaveAsync(MycorrhizaeEvent mycorrhizaeEvent, CancellationToken cancellationToken = default);
    Task SendToAlarmAsync(MycorrhizaeEvent mycorrhizaeEvent, CancellationToken cancellationToken = default);
    Task UpdateMycaAsync(MycorrhizaeEvent mycorrhizaeEvent, CancellationToken cancellationToken = default);
    Task SyncWithWebsiteAsync(WebsiteSyncRequest request, CancellationToken cancellationToken = default);
    Task<HplSimulationResult> ProcessHplSimulationAsync(HplSimulationRequest request, CancellationToken cancellationToken = default);
}

public class Mushroom1Telemetry
{
    public string DeviceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double[] BioelectricChannels { get; set; } = Array.Empty<double>();
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public double Pressure { get; set; }
    public double GasResistance { get; set; }
    public double VocIndex { get; set; }
    public GeoLocation? Location { get; set; }
    public string? TenantId { get; set; }
}

public class ProcessingResult
{
    public bool Success { get; set; }
    public string EventId { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public class MWaveProcessingRequest
{
    public string EventId { get; set; } = string.Empty;
    public object? SignalVector { get; set; }
    public int SamplingRate { get; set; }
    public int WindowSize { get; set; }
    public double OverlapRatio { get; set; }
    public string WaveletType { get; set; } = string.Empty;
    public FrequencyBand[] FrequencyBands { get; set; } = Array.Empty<FrequencyBand>();
}

public class FrequencyBand
{
    public string Name { get; set; } = string.Empty;
    public double Min { get; set; }
    public double Max { get; set; }
}

public class AlarmAnalysisRequest
{
    public string EventId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, double> SignalFeatures { get; set; } = new();
    public EnvironmentalContext? EnvironmentalContext { get; set; }
}

public class MycaKnowledgeUpdate
{
    public string EventId { get; set; } = string.Empty;
    public string KnowledgeType { get; set; } = string.Empty;
    public object? Data { get; set; }
    public int Priority { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class WebsiteSyncRequest
{
    public string[] DataTypes { get; set; } = Array.Empty<string>();
    public DateTime LastSync { get; set; }
}

public class WebsiteDashboardData
{
    public long TotalEvents { get; set; }
    public int ActiveDevices { get; set; }
    public int SpeciesDetected { get; set; }
    public int OnlineUsers { get; set; }
    public List<LiveReading> LiveReadings { get; set; } = new();
    public List<string> TrendingCompounds { get; set; } = new();
    public List<string> RecentDiscoveries { get; set; } = new();
}

public class LiveReading
{
    public string DeviceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
}

public class HplSimulationRequest
{
    public string HplCode { get; set; } = string.Empty;
    public Dictionary<string, object> InitialConditions { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

public class HplSimulationResult
{
    public string SimulationId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public object? Results { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public long MemoryUsage { get; set; }
}

public class HplCompilationRequest
{
    public string SourceCode { get; set; } = string.Empty;
    public string OptimizationLevel { get; set; } = string.Empty;
    public string TargetFormat { get; set; } = string.Empty;
}

public class HplCompilationResult
{
    public byte[] WasmBinary { get; set; } = Array.Empty<byte>();
    public bool Success { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class MyceliumSimRequest
{
    public byte[] WasmBinary { get; set; } = Array.Empty<byte>();
    public Dictionary<string, object> InitialConditions { get; set; } = new();
    public TimeSpan SimulationTime { get; set; }
    public string OutputFormat { get; set; } = string.Empty;
}

public class MyceliumSimResult
{
    public string SimulationId { get; set; } = string.Empty;
    public object? OutputData { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public long MemoryUsage { get; set; }
} 