namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service interface for lab-specific operations
/// </summary>
public interface ILabToolsService
{
    /// <summary>
    /// Get list of lab samples
    /// </summary>
    Task<IEnumerable<LabSample>> GetSamplesAsync(string? filter = null, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a sample by ID
    /// </summary>
    Task<LabSample?> GetSampleAsync(string sampleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Register a new sample
    /// </summary>
    Task<LabSample> RegisterSampleAsync(LabSample sample, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get available protocols
    /// </summary>
    Task<IEnumerable<LabProtocol>> GetProtocolsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get protocol by ID
    /// </summary>
    Task<LabProtocol?> GetProtocolAsync(string protocolId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Lab sample
/// </summary>
public class LabSample
{
    public string SampleId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Species { get; set; }
    public string? SubstrateType { get; set; }
    public DateTime CollectedAt { get; set; }
    public string? Location { get; set; }
    public string Status { get; set; } = "Pending";
}

/// <summary>
/// Lab protocol
/// </summary>
public class LabProtocol
{
    public string ProtocolId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int StepCount { get; set; }
}
