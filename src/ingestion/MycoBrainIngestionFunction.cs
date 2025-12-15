using Azure.Messaging.EventGrid;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using NatureOS.MINDEX.Models;
using NatureOS.Mycorrhizae;
using System.Text;
using System.Text.Json;

namespace NatureOS.Ingestion;

/// <summary>
/// Ingest MycoBrain telemetry from MAS/Gateway queues.
/// </summary>
public class MycoBrainIngestionFunction
{
    private readonly CosmosClient _cosmosClient;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly EventGridPublisherClient _eventGridClient;
    private readonly ILogger<MycoBrainIngestionFunction> _logger;

    public MycoBrainIngestionFunction(
        CosmosClient cosmosClient,
        ServiceBusClient serviceBusClient,
        EventGridPublisherClient eventGridClient,
        ILogger<MycoBrainIngestionFunction> logger)
    {
        _cosmosClient = cosmosClient;
        _serviceBusClient = serviceBusClient;
        _eventGridClient = eventGridClient;
        _logger = logger;
    }

    [Function("MycoBrainIngestNDJSON")]
    public async Task IngestNdjson(
        [ServiceBusTrigger("mycobrain-telemetry", Connection = "ServiceBusConnectionString")] string jsonLine)
    {
        var telemetry = MDPv1Protocol.ParseNDJSON(jsonLine);
        if (telemetry == null)
        {
            _logger.LogWarning("Failed to parse MycoBrain NDJSON");
            return;
        }

        await PersistTelemetryAsync(telemetry);
    }

    [Function("MycoBrainIngestMDP")]
    public async Task IngestMdpFrame(
        [ServiceBusTrigger("mycobrain-mdp-frames", Connection = "ServiceBusConnectionString")] byte[] frame)
    {
        var (type, payload, valid) = MDPv1Protocol.DecodeMessage(frame);
        if (!valid)
        {
            _logger.LogWarning("Invalid MDP frame (CRC/COBS)");
            return;
        }

        if (type != MDPv1Protocol.MessageType.Telemetry)
            return;

        var json = Encoding.UTF8.GetString(payload);
        var telemetry = JsonSerializer.Deserialize<MycoBrainTelemetry>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (telemetry == null)
        {
            _logger.LogWarning("Failed to parse telemetry payload");
            return;
        }

        await PersistTelemetryAsync(telemetry);
    }

    private async Task PersistTelemetryAsync(MycoBrainTelemetry telemetry)
    {
        var database = _cosmosClient.GetDatabase("mindex");
        var events = database.GetContainer("events");
        var telemetryContainer = database.GetContainer("mycobrain_telemetry");
        var devices = database.GetContainer("devices");

        var eventId = $"{telemetry.SerialNumber}-{telemetry.SequenceNumber}";
        var ev = new MycorrhizaeEvent
        {
            EventId = eventId,
            Timestamp = DateTime.UtcNow,
            SourceDevice = telemetry.SerialNumber,
            KingdomDomain = "FUNGA.environmental",
            SignalVector = telemetry,
            Metadata = new EventMetadata
            {
                PipelineVersion = "mycobrain-ingestion",
                IngestedAt = DateTime.UtcNow,
                Flags = new List<string> { "mycobrain" }
            },
            TTL = 86400
        };

        try
        {
            await events.CreateItemAsync(ev, new PartitionKey(telemetry.SerialNumber));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogInformation("Duplicate telemetry skipped {EventId}", eventId);
            return;
        }

        await telemetryContainer.CreateItemAsync(telemetry, new PartitionKey(telemetry.SerialNumber));

        // best-effort device upsert
        var deviceDoc = new
        {
            device_id = telemetry.SerialNumber,
            device_type = "mycobrain",
            firmware_version = telemetry.FirmwareVersion,
            last_seen = DateTime.UtcNow,
            status = "Online",
            i2c_addresses = telemetry.SideA?.I2CDevices ?? new List<string>()
        };
        await devices.UpsertItemAsync(deviceDoc, new PartitionKey(telemetry.SerialNumber));

        await PublishEventGridAsync(ev);
        await PublishServiceBusAsync(ev);

        _logger.LogInformation("Ingested MycoBrain telemetry {Serial} seq={Seq}", telemetry.SerialNumber, telemetry.SequenceNumber);
    }

    private async Task PublishEventGridAsync(MycorrhizaeEvent ev)
    {
        try
        {
            var e = new EventGridEvent(
                subject: $"natureos/mycobrain/{ev.SourceDevice}",
                eventType: "NatureOS.MycoBrain.Telemetry",
                dataVersion: "1.0",
                data: BinaryData.FromObjectAsJson(ev))
            {
                Id = ev.EventId,
                EventTime = ev.Timestamp
            };

            await _eventGridClient.SendEventAsync(e);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EventGrid publish failed");
        }
    }

    private async Task PublishServiceBusAsync(MycorrhizaeEvent ev)
    {
        try
        {
            var sender = _serviceBusClient.CreateSender("mycorrhizae-events");
            await sender.SendMessageAsync(new ServiceBusMessage(JsonSerializer.Serialize(ev))
            {
                MessageId = ev.EventId,
                Subject = "mycobrain.telemetry",
                CorrelationId = ev.SourceDevice
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServiceBus publish failed");
        }
    }
}
