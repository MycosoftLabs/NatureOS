using NatureOS.MINDEX.Models;

namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service interface for ingesting NatureOS events and activity into MAS knowledge graph
/// </summary>
public interface IMasIngestionService
{
    /// <summary>
    /// Ingest a Mycorrhizae event into the MAS knowledge graph projection
    /// </summary>
    Task<bool> IngestEventAsync(MycorrhizaeEvent mycorrhizaeEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ingest arbitrary context (website actions, analytics, user signals) for MAS learning
    /// </summary>
    Task<bool> IngestContextAsync(object contextPayload, CancellationToken cancellationToken = default);
}
