using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using NatureOS.MINDEX.Models;
using System.Text.Json;

namespace NatureOS.Ingestion;

/// <summary>
/// Functions to ingest NatureOS events into MAS via Cosmos change feed and Service Bus
/// </summary>
public class MasIngestionFunctions
{
    private readonly CosmosClient _cosmosClient;
    private readonly ILogger<MasIngestionFunctions> _logger;

    public MasIngestionFunctions(CosmosClient cosmosClient, ILogger<MasIngestionFunctions> logger)
    {
        _cosmosClient = cosmosClient;
        _logger = logger;
    }

    // Cosmos DB change feed trigger on MINDEX/events container
    [Function("MasIngestFromChangeFeed")]
    public async Task MasIngestFromChangeFeed(
        [CosmosDBTrigger(
            databaseName: "mindex",
            containerName: "events",
            Connection = "CosmosDbConnectionString",
            LeaseContainerName = "leases",
            CreateLeaseContainerIfNotExists = true)]
        IReadOnlyList<MycorrhizaeEvent> input)
    {
        if (input == null || input.Count == 0) return;

        try
        {
            var database = _cosmosClient.GetDatabase("mindex");
            var masContainer = database.GetContainer("mas_events");
            foreach (var ev in input)
            {
                var projection = new
                {
                    id = ev.EventId,
                    type = "event_projection",
                    timestamp = ev.Timestamp,
                    source_device = ev.SourceDevice,
                    kingdom_domain = ev.KingdomDomain,
                    signal_vector = ev.SignalVector,
                    decoded_meaning = ev.DecodedMeaning,
                    references = ev.References,
                    metadata = ev.Metadata
                };

                await masContainer.CreateItemAsync(projection, new PartitionKey(ev.SourceDevice));
            }

            _logger.LogInformation("MAS ingestion via change feed projected {Count} events", input.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MAS ingestion via change feed failed");
            throw;
        }
    }
}
