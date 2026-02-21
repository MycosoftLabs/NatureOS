namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service for lab-specific operations
/// </summary>
public class LabToolsService : ILabToolsService
{
    private readonly ILogger<LabToolsService> _logger;
    private readonly List<LabSample> _samples = new();
    private readonly List<LabProtocol> _protocols = new();
    private static readonly object _lock = new();

    public LabToolsService(ILogger<LabToolsService> logger)
    {
        _logger = logger;
        _protocols.AddRange(new[]
        {
            new LabProtocol { ProtocolId = "ext-001", Name = "DNA Extraction", Description = "Standard DNA extraction", StepCount = 12 },
            new LabProtocol { ProtocolId = "seq-001", Name = "Sequencing Prep", Description = "Library prep for sequencing", StepCount = 8 },
            new LabProtocol { ProtocolId = "cult-001", Name = "Culture Isolation", Description = "Fungal culture isolation", StepCount = 15 }
        });
    }

    public Task<IEnumerable<LabSample>> GetSamplesAsync(string? filter = null, int limit = 100, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var result = _samples.AsEnumerable();
            if (!string.IsNullOrEmpty(filter))
                result = result.Where(s => s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    (s.Species?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false));
            return Task.FromResult(result.Take(limit));
        }
    }

    public Task<LabSample?> GetSampleAsync(string sampleId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_samples.FirstOrDefault(s => s.SampleId == sampleId));
        }
    }

    public Task<LabSample> RegisterSampleAsync(LabSample sample, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sample.SampleId))
            sample.SampleId = $"SMP-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        lock (_lock)
        {
            _samples.Add(sample);
        }
        return Task.FromResult(sample);
    }

    public Task<IEnumerable<LabProtocol>> GetProtocolsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_protocols.AsEnumerable());

    public Task<LabProtocol?> GetProtocolAsync(string protocolId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_protocols.FirstOrDefault(p => p.ProtocolId == protocolId));
}
