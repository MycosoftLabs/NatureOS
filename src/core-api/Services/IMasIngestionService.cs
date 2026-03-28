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
    Task<MasIngestionResult> IngestEventAsync(MycorrhizaeEvent mycorrhizaeEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ingest arbitrary context (website actions, analytics, user signals) for MAS learning
    /// </summary>
    Task<MasIngestionResult> IngestContextAsync(object contextPayload, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a MAS ingestion operation with explicit success/failure semantics
/// </summary>
public class MasIngestionResult
{
    public bool Success { get; set; }
    public string? DocumentId { get; set; }
    public string? Error { get; set; }
    public DateTime Timestamp { get; set; }

    public static MasIngestionResult Ok(string documentId) => new()
    {
        Success = true,
        DocumentId = documentId,
        Timestamp = DateTime.UtcNow
    };

    public static MasIngestionResult Fail(string error) => new()
    {
        Success = false,
        Error = error,
        Timestamp = DateTime.UtcNow
    };
}
