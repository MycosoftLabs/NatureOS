using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using NatureOS.MINDEX.Models;
using System.Net;

namespace NatureOS.CoreApi.Services;

/// <summary>
/// Minimal MAS ingestion service that projects events and context to MAS containers
/// </summary>
public class MasIngestionService : IMasIngestionService
{
    private readonly CosmosClient _cosmosClient;
    private readonly ILogger<MasIngestionService> _logger;

    public MasIngestionService(CosmosClient cosmosClient, ILogger<MasIngestionService> logger)
    {
        _cosmosClient = cosmosClient;
        _logger = logger;
    }

    public async Task<MasIngestionResult> IngestEventAsync(MycorrhizaeEvent mycorrhizaeEvent, CancellationToken cancellationToken = default)
    {
        if (mycorrhizaeEvent == null)
            return MasIngestionResult.Fail("Payload is null");

        if (string.IsNullOrWhiteSpace(mycorrhizaeEvent.EventId))
            return MasIngestionResult.Fail("EventId is missing");

        if (string.IsNullOrWhiteSpace(mycorrhizaeEvent.SourceDevice))
            return MasIngestionResult.Fail("SourceDevice is missing");

        try
        {
            var database = _cosmosClient.GetDatabase("mindex");
            var container = database.GetContainer("mas_events");

            var projection = new
            {
                id = mycorrhizaeEvent.EventId,
                type = "event_projection",
                timestamp = mycorrhizaeEvent.Timestamp,
                source_device = mycorrhizaeEvent.SourceDevice,
                kingdom_domain = mycorrhizaeEvent.KingdomDomain,
                signal_vector = mycorrhizaeEvent.SignalVector,
                decoded_meaning = mycorrhizaeEvent.DecodedMeaning,
                references = mycorrhizaeEvent.References,
                metadata = mycorrhizaeEvent.Metadata
            };

            await container.CreateItemAsync(projection, new PartitionKey(mycorrhizaeEvent.SourceDevice), cancellationToken: cancellationToken);
            _logger.LogInformation("Projected event {EventId} into MAS graph container", mycorrhizaeEvent.EventId);
            return MasIngestionResult.Ok(mycorrhizaeEvent.EventId);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            // Idempotent: duplicate event already ingested
            _logger.LogWarning("Duplicate event {EventId} already exists in MAS", mycorrhizaeEvent.EventId);
            return MasIngestionResult.Ok(mycorrhizaeEvent.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest event {EventId} into MAS", mycorrhizaeEvent.EventId);
            return MasIngestionResult.Fail($"Ingestion failed: {ex.Message}");
        }
    }

    public async Task<MasIngestionResult> IngestContextAsync(object contextPayload, CancellationToken cancellationToken = default)
    {
        if (contextPayload == null)
            return MasIngestionResult.Fail("Payload is null");

        try
        {
            var database = _cosmosClient.GetDatabase("mindex");
            var container = database.GetContainer("mas_context");

            var documentId = Ulid.NewUlid().ToString();
            var document = new
            {
                id = documentId,
                type = "context_signal",
                timestamp = DateTime.UtcNow,
                payload = contextPayload
            };

            await container.CreateItemAsync(document, new PartitionKey("context"), cancellationToken: cancellationToken);
            _logger.LogInformation("Ingested context payload {DocumentId} into MAS container", documentId);
            return MasIngestionResult.Ok(documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest context into MAS");
            return MasIngestionResult.Fail($"Context ingestion failed: {ex.Message}");
        }
    }
}
