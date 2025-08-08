using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using NatureOS.MINDEX.Models;

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

    public async Task<bool> IngestEventAsync(MycorrhizaeEvent mycorrhizaeEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _cosmosClient.GetDatabase("MINDEX");
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
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest event into MAS");
            return false;
        }
    }

    public async Task<bool> IngestContextAsync(object contextPayload, CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _cosmosClient.GetDatabase("MINDEX");
            var container = database.GetContainer("mas_context");

            var document = new
            {
                id = Ulid.NewUlid().ToString(),
                type = "context_signal",
                timestamp = DateTime.UtcNow,
                payload = contextPayload
            };

            await container.CreateItemAsync(document, new PartitionKey("context"), cancellationToken: cancellationToken);
            _logger.LogInformation("Ingested context payload into MAS container");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest context into MAS");
            return false;
        }
    }
}
