namespace NatureOS.CoreApi.Services;

/// <summary>
/// Interface for Mycosoft ecosystem integration services
/// </summary>
public interface IMycosoftIntegrationService
{
    /// <summary>
    /// Process Mushroom 1 device telemetry with real-time broadcasting
    /// </summary>
    Task<ProcessingResult> ProcessMushroom1TelemetryAsync(object telemetryData);

    /// <summary>
    /// Process MYCA query with enhanced system context
    /// </summary>
    Task<MycaResponse> ProcessMycaQueryAsync(string enhancedQuery, string userId);

    /// <summary>
    /// Execute HPL simulation with real-time updates
    /// </summary>
    Task<HplSimulationResult> ExecuteHplSimulationAsync(object simulationData);

    /// <summary>
    /// Synchronize data across the entire Mycosoft ecosystem
    /// </summary>
    Task<SynchronizationResult> SynchronizeEcosystemAsync();
} 