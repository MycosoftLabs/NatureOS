using Azure.Messaging.EventGrid;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.Cosmos;
using NatureOS.CoreApi.Hubs;
using NatureOS.MINDEX.Models;
using NatureOS.Mycorrhizae;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NatureOS.CoreApi.Services;

public class MycoBrainService : IMycoBrainService
{
    private readonly CosmosClient _cosmosClient;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly EventGridPublisherClient _eventGridClient;
    private readonly IHubContext<NatureOSHub> _hubContext;
    private readonly ILogger<MycoBrainService> _logger;

    public MycoBrainService(
        CosmosClient cosmosClient,
        ServiceBusClient serviceBusClient,
        EventGridPublisherClient eventGridClient,
        IHubContext<NatureOSHub> hubContext,
        ILogger<MycoBrainService> logger)
    {
        _cosmosClient = cosmosClient;
        _serviceBusClient = serviceBusClient;
        _eventGridClient = eventGridClient;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<ProcessingResult> ProcessTelemetryAsync(MycoBrainTelemetry telemetry, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MycoBrain telemetry {Serial} seq={Seq}", telemetry.SerialNumber, telemetry.SequenceNumber);

        var eventId = $"{telemetry.SerialNumber}-{telemetry.SequenceNumber}";
        var mycorrhizaeEvent = ConvertToMycorrhizaeEvent(telemetry, eventId);

        var database = _cosmosClient.GetDatabase("mindex");
        var events = database.GetContainer("events");
        var telemetryContainer = database.GetContainer("mycobrain_telemetry");

        // idempotent insert by eventId
        try
        {
            await events.CreateItemAsync(mycorrhizaeEvent, new PartitionKey(telemetry.SerialNumber), cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            return new ProcessingResult { Success = true, EventId = eventId, Timestamp = DateTime.UtcNow, Message = "Duplicate telemetry skipped (idempotent)" };
        }

        await telemetryContainer.CreateItemAsync(telemetry, new PartitionKey(telemetry.SerialNumber), cancellationToken: cancellationToken);
        await UpsertDeviceFromTelemetryAsync(telemetry, cancellationToken);

        await PublishToEventGridAsync(mycorrhizaeEvent);
        await SendToServiceBusAsync(mycorrhizaeEvent);

        await _hubContext.Clients.Group("DashboardUsers").SendAsync("DeviceUpdate", new
        {
            DeviceId = telemetry.SerialNumber,
            DeviceType = "mycobrain",
            Data = telemetry,
            Timestamp = DateTime.UtcNow,
            Status = "Updated"
        }, cancellationToken);

        return new ProcessingResult { Success = true, EventId = eventId, Timestamp = DateTime.UtcNow, Message = "Telemetry processed" };
    }

    public async Task<ProcessingResult> ProcessNDJSONLineAsync(string jsonLine, CancellationToken cancellationToken = default)
    {
        var telemetry = MDPv1Protocol.ParseNDJSON(jsonLine);
        if (telemetry == null)
            return new ProcessingResult { Success = false, Timestamp = DateTime.UtcNow, Message = "Failed to parse NDJSON" };

        return await ProcessTelemetryAsync(telemetry, cancellationToken);
    }

    public async Task<ProcessingResult> ProcessMDPFrameAsync(byte[] frame, CancellationToken cancellationToken = default)
    {
        var (type, payload, valid) = MDPv1Protocol.DecodeMessage(frame);
        if (!valid)
            return new ProcessingResult { Success = false, Timestamp = DateTime.UtcNow, Message = "Invalid MDP frame" };

        if (type != MDPv1Protocol.MessageType.Telemetry)
            return new ProcessingResult { Success = true, Timestamp = DateTime.UtcNow, Message = $"Ignored {type}" };

        var json = Encoding.UTF8.GetString(payload);
        try
        {
            var telemetry = JsonSerializer.Deserialize<MycoBrainTelemetry>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (telemetry != null)
                return await ProcessTelemetryAsync(telemetry, cancellationToken);
        }
        catch
        {
            // Fall through to envelope path.
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return await ProcessEnvelopeAsync(doc.RootElement, cancellationToken);
        }
        catch
        {
            return new ProcessingResult { Success = false, Timestamp = DateTime.UtcNow, Message = "Failed to parse telemetry payload" };
        }
    }

    public async Task<ProcessingResult> ProcessEnvelopeAsync(JsonElement envelope, CancellationToken cancellationToken = default)
    {
        try
        {
            // Minimal structural validation (full signature verification happens in Mycorrhizae/MAS).
            if (!envelope.TryGetProperty("hdr", out var hdr) || hdr.ValueKind != JsonValueKind.Object)
                return new ProcessingResult { Success = false, Timestamp = DateTime.UtcNow, Message = "Invalid envelope: missing hdr" };

            var deviceId = hdr.TryGetProperty("deviceId", out var d) ? d.GetString() : null;
            var msgId = hdr.TryGetProperty("msgId", out var m) ? m.GetString() : null;
            var seq = envelope.TryGetProperty("seq", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt32() : -1;

            if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(msgId) || seq < 0)
                return new ProcessingResult { Success = false, Timestamp = DateTime.UtcNow, Message = "Invalid envelope: missing deviceId/msgId/seq" };

            var mindexUrl = (Environment.GetEnvironmentVariable("MINDEX_API_URL") ?? "http://192.168.0.189:8000").TrimEnd('/');
            var mindexApiKey = Environment.GetEnvironmentVariable("MINDEX_API_KEY") ?? "";
            if (string.IsNullOrWhiteSpace(mindexApiKey))
                return new ProcessingResult { Success = false, Timestamp = DateTime.UtcNow, Message = "MINDEX_API_KEY not configured" };

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.Add("X-API-Key", mindexApiKey);

            var body = JsonSerializer.Serialize(new { envelope, verified_by = "natureos" });
            var resp = await http.PostAsync(
                $"{mindexUrl}/api/telemetry/envelope",
                new StringContent(body, Encoding.UTF8, "application/json"),
                cancellationToken
            );

            var respText = await resp.Content.ReadAsStringAsync(cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("MINDEX envelope ingest failed {Status}: {Body}", (int)resp.StatusCode, respText);
                return new ProcessingResult { Success = false, Timestamp = DateTime.UtcNow, EventId = $"{deviceId}-{seq}", Message = "MINDEX ingest failed" };
            }

            // Broadcast envelope ingestion to dashboard clients (real-time).
            await _hubContext.Clients.Group("DashboardUsers").SendAsync("DeviceEnvelope", new
            {
                DeviceId = deviceId,
                MsgId = msgId,
                Seq = seq,
                Envelope = envelope,
                Timestamp = DateTime.UtcNow,
                Status = "Ingested"
            }, cancellationToken);

            return new ProcessingResult { Success = true, Timestamp = DateTime.UtcNow, EventId = $"{deviceId}-{seq}", Message = "Envelope ingested" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process envelope telemetry");
            return new ProcessingResult { Success = false, Timestamp = DateTime.UtcNow, Message = "Envelope processing failed" };
        }
    }

    public async Task<bool> SendCommandAsync(MycoBrainCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var frame = MDPv1Protocol.BuildCommandFrame(command);
            var sender = _serviceBusClient.CreateSender("mycobrain-commands");
            await sender.SendMessageAsync(new ServiceBusMessage(frame)
            {
                MessageId = $"{command.TargetSerial}-{command.SequenceNumber}",
                Subject = command.CommandId.ToString(),
                CorrelationId = command.TargetSerial
            }, cancellationToken);

            await _hubContext.Clients.Group("DashboardUsers").SendAsync("DeviceCommand", new
            {
                DeviceId = command.TargetSerial,
                DeviceType = "mycobrain",
                CommandId = command.CommandId.ToString(),
                Sequence = command.SequenceNumber,
                Timestamp = DateTime.UtcNow
            }, cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send command to {Serial}", command.TargetSerial);
            return false;
        }
    }

    public async Task<MycoBrainDevice> RegisterDeviceAsync(MycoBrainDevice device, CancellationToken cancellationToken = default)
    {
        var database = _cosmosClient.GetDatabase("mindex");
        var devices = database.GetContainer("devices");

        device.CreatedAt = device.CreatedAt == default ? DateTime.UtcNow : device.CreatedAt;
        device.LastSeen = DateTime.UtcNow;
        if (device.Status == DeviceStatus.Unknown)
            device.Status = DeviceStatus.Online;

        await devices.UpsertItemAsync(device, new PartitionKey(device.DeviceId), cancellationToken: cancellationToken);
        return device;
    }

    public async Task<MycoBrainDevice?> GetDeviceAsync(string serialNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _cosmosClient.GetDatabase("mindex");
            var devices = database.GetContainer("devices");
            var response = await devices.ReadItemAsync<MycoBrainDevice>(serialNumber, new PartitionKey(serialNumber), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IEnumerable<MycoBrainDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        var database = _cosmosClient.GetDatabase("mindex");
        var devices = database.GetContainer("devices");

        var query = new QueryDefinition("SELECT * FROM c WHERE c.device_type = @t").WithParameter("@t", "mycobrain");
        var iterator = devices.GetItemQueryIterator<MycoBrainDevice>(query);
        var results = new List<MycoBrainDevice>();

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(page);
        }

        return results;
    }

    public async Task<MycoBrainDevice> UpdateDeviceAsync(MycoBrainDevice device, CancellationToken cancellationToken = default)
    {
        var database = _cosmosClient.GetDatabase("mindex");
        var devices = database.GetContainer("devices");
        var response = await devices.UpsertItemAsync(device, new PartitionKey(device.DeviceId), cancellationToken: cancellationToken);
        return response.Resource;
    }

    public async Task<IEnumerable<MycoBrainTelemetry>> GetTelemetryHistoryAsync(string serialNumber, DateTime? startTime = null, DateTime? endTime = null, CancellationToken cancellationToken = default)
    {
        var database = _cosmosClient.GetDatabase("mindex");
        var telemetry = database.GetContainer("mycobrain_telemetry");

        var start = startTime.HasValue ? (long)(startTime.Value - DateTime.UnixEpoch).TotalMilliseconds : 0;
        var end = endTime.HasValue ? (long)(endTime.Value - DateTime.UnixEpoch).TotalMilliseconds : long.MaxValue;

        var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.serial = @s AND c.ts >= @start AND c.ts <= @end ORDER BY c.ts DESC")
            .WithParameter("@s", serialNumber)
            .WithParameter("@start", start)
            .WithParameter("@end", end);

        var iterator = telemetry.GetItemQueryIterator<MycoBrainTelemetry>(query, requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(serialNumber)
        });

        var results = new List<MycoBrainTelemetry>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(page);
        }

        return results;
    }

    private static MycorrhizaeEvent ConvertToMycorrhizaeEvent(MycoBrainTelemetry telemetry, string eventId)
    {
        var signal = new Dictionary<string, object?>
        {
            ["serial"] = telemetry.SerialNumber,
            ["fw_version"] = telemetry.FirmwareVersion,
            ["ts"] = telemetry.DeviceTimestamp,
            ["side_a"] = telemetry.SideA,
            ["side_b"] = telemetry.SideB,
            ["power"] = telemetry.Power
        };

        return new MycorrhizaeEvent
        {
            EventId = eventId,
            Timestamp = DateTime.UtcNow,
            SourceDevice = telemetry.SerialNumber,
            KingdomDomain = "FUNGA.environmental",
            SignalVector = signal,
            DecodedMeaning = new DecodedMeaning
            {
                Type = "mycobrain_telemetry",
                Confidence = 1.0,
                Ontology = new Dictionary<string, object> { ["mdp_seq"] = telemetry.SequenceNumber }
            },
            Metadata = new EventMetadata
            {
                PipelineVersion = "mycobrain",
                IngestedAt = DateTime.UtcNow,
                QualityScore = 1.0,
                Flags = new List<string> { "mycobrain" }
            },
            TTL = 86400
        };
    }

    private async Task UpsertDeviceFromTelemetryAsync(MycoBrainTelemetry telemetry, CancellationToken cancellationToken)
    {
        var existing = await GetDeviceAsync(telemetry.SerialNumber, cancellationToken);
        if (existing == null)
        {
            await RegisterDeviceAsync(new MycoBrainDevice
            {
                DeviceId = telemetry.SerialNumber,
                FirmwareVersion = telemetry.FirmwareVersion,
                I2CAddresses = telemetry.SideA?.I2CDevices ?? new List<string>(),
                Status = DeviceStatus.Online,
                CreatedAt = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow
            }, cancellationToken);
            return;
        }

        existing.LastSeen = DateTime.UtcNow;
        existing.Status = DeviceStatus.Online;
        existing.FirmwareVersion = telemetry.FirmwareVersion;
        if (telemetry.SideA?.I2CDevices != null)
            existing.I2CAddresses = telemetry.SideA.I2CDevices;

        await UpdateDeviceAsync(existing, cancellationToken);
    }

    private async Task PublishToEventGridAsync(MycorrhizaeEvent ev)
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

    private async Task SendToServiceBusAsync(MycorrhizaeEvent ev)
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

