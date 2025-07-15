using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using Azure.Messaging.EventGrid;
using Azure.Messaging.ServiceBus;
using NatureOS.MINDEX.Models;
using System.Text.Json;

namespace NatureOS.Ingestion;

/// <summary>
/// Azure Function for ingesting IoT telemetry into the NatureOS event pipeline
/// </summary>
public class IngestionFunction
{
    private readonly CosmosClient _cosmosClient;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly EventGridPublisherClient _eventGridClient;
    private readonly ILogger<IngestionFunction> _logger;

    public IngestionFunction(
        CosmosClient cosmosClient,
        ServiceBusClient serviceBusClient,
        EventGridPublisherClient eventGridClient,
        ILogger<IngestionFunction> logger)
    {
        _cosmosClient = cosmosClient;
        _serviceBusClient = serviceBusClient;
        _eventGridClient = eventGridClient;
        _logger = logger;
    }

    /// <summary>
    /// Process IoT Hub messages and convert them to Mycorrhizae Protocol events
    /// </summary>
    /// <param name="message">IoT Hub message</param>
    /// <param name="context">Function context</param>
    [Function("ProcessIoTMessage")]
    public async Task ProcessIoTMessage(
        [EventHubTrigger("messages/events", Connection = "IoTHubConnectionString")] string[] messages,
        FunctionContext context)
    {
        var logger = context.GetLogger("ProcessIoTMessage");
        
        try
        {
            logger.LogInformation("Processing {Count} IoT messages", messages.Length);

            foreach (var message in messages)
            {
                // Parse the IoT message
                var iotMessage = JsonSerializer.Deserialize<IoTMessage>(message);
                if (iotMessage == null)
                {
                    logger.LogWarning("Failed to parse IoT message: {Message}", message);
                    continue;
                }

                // Convert to Mycorrhizae Protocol event
                var mycorrhizaeEvent = ConvertToMycorrhizaeEvent(iotMessage);

                // Store in MINDEX
                await StoreEventAsync(mycorrhizaeEvent);

                // Publish to Event Grid for downstream processing
                await PublishEventAsync(mycorrhizaeEvent);

                // Send to Mycorrhizae processing queue
                await SendToProcessingQueueAsync(mycorrhizaeEvent);

                logger.LogInformation("Successfully processed IoT message from device {DeviceId}", iotMessage.DeviceId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process IoT messages");
            throw; // Let Azure Functions handle retries
        }
    }

    /// <summary>
    /// HTTP endpoint for manual event ingestion
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <param name="context">Function context</param>
    /// <returns>HTTP response</returns>
    [Function("IngestEvent")]
    public async Task<HttpResponseData> IngestEvent(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        FunctionContext context)
    {
        var logger = context.GetLogger("IngestEvent");
        
        try
        {
            // Read the request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            
            // Parse the Mycorrhizae event
            var mycorrhizaeEvent = JsonSerializer.Deserialize<MycorrhizaeEvent>(requestBody);
            if (mycorrhizaeEvent == null)
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid event format");
                return badRequestResponse;
            }

            // Validate required fields
            if (string.IsNullOrEmpty(mycorrhizaeEvent.SourceDevice) || 
                string.IsNullOrEmpty(mycorrhizaeEvent.KingdomDomain))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("SourceDevice and KingdomDomain are required");
                return badRequestResponse;
            }

            // Process the event
            await ProcessMycorrhizaeEventAsync(mycorrhizaeEvent);

            // Return success response
            var response = req.CreateResponse(System.Net.HttpStatusCode.Created);
            await response.WriteAsJsonAsync(new { 
                message = "Event ingested successfully", 
                eventId = mycorrhizaeEvent.EventId 
            });
            
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ingest event via HTTP");
            
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Internal server error");
            return errorResponse;
        }
    }

    /// <summary>
    /// Process Service Bus messages for enrichment pipeline
    /// </summary>
    /// <param name="message">Service Bus message</param>
    /// <param name="context">Function context</param>
    [Function("ProcessEnrichmentMessage")]
    public async Task ProcessEnrichmentMessage(
        [ServiceBusTrigger("mycorrhizae-events", Connection = "ServiceBusConnectionString")] 
        ServiceBusReceivedMessage message,
        FunctionContext context)
    {
        var logger = context.GetLogger("ProcessEnrichmentMessage");
        
        try
        {
            var messageBody = message.Body.ToString();
            logger.LogInformation("Processing enrichment message: {Message}", messageBody);

            var mycorrhizaeEvent = JsonSerializer.Deserialize<MycorrhizaeEvent>(messageBody);
            if (mycorrhizaeEvent == null)
            {
                logger.LogWarning("Failed to parse enrichment message: {Message}", messageBody);
                return;
            }

            // Perform semantic enrichment
            await EnrichEventAsync(mycorrhizaeEvent);

            logger.LogInformation("Successfully processed enrichment for event {EventId}", mycorrhizaeEvent.EventId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process enrichment message");
            throw;
        }
    }

    private MycorrhizaeEvent ConvertToMycorrhizaeEvent(IoTMessage iotMessage)
    {
        var eventId = Ulid.NewUlid().ToString();
        var timestamp = DateTime.UtcNow;

        return new MycorrhizaeEvent
        {
            EventId = eventId,
            Timestamp = timestamp,
            SourceDevice = iotMessage.DeviceId,
            KingdomDomain = DetermineKingdomDomain(iotMessage),
            SignalVector = iotMessage.Telemetry,
            References = new EventReferences
            {
                Location = iotMessage.Location
            },
            Metadata = new EventMetadata
            {
                IngestedAt = timestamp,
                PipelineVersion = "1.0.0",
                TenantId = iotMessage.TenantId,
                QualityScore = CalculateQualityScore(iotMessage)
            }
        };
    }

    private static string DetermineKingdomDomain(IoTMessage iotMessage)
    {
        // Determine kingdom domain based on device type or sensor data
        if (iotMessage.DeviceType?.StartsWith("mushroom") == true)
        {
            return "FUNGA.signaling";
        }
        
        if (iotMessage.DeviceType?.StartsWith("spore") == true)
        {
            return "FUNGA.reproduction";
        }

        // Default to FUNGA for now
        return "FUNGA.unknown";
    }

    private static double CalculateQualityScore(IoTMessage iotMessage)
    {
        var score = 1.0;

        // Deduct points for missing data
        if (iotMessage.Location == null) score -= 0.2;
        if (string.IsNullOrEmpty(iotMessage.DeviceType)) score -= 0.1;
        if (iotMessage.Telemetry == null) score -= 0.5;

        return Math.Max(0.0, score);
    }

    private async Task StoreEventAsync(MycorrhizaeEvent mycorrhizaeEvent)
    {
        var database = _cosmosClient.GetDatabase("mindex");
        var container = database.GetContainer("events");

        await container.CreateItemAsync(
            mycorrhizaeEvent,
            new PartitionKey(mycorrhizaeEvent.SourceDevice));

        _logger.LogInformation("Stored event {EventId} in MINDEX", mycorrhizaeEvent.EventId);
    }

    private async Task PublishEventAsync(MycorrhizaeEvent mycorrhizaeEvent)
    {
        var eventGridEvent = new EventGridEvent(
            subject: $"devices/{mycorrhizaeEvent.SourceDevice}",
            eventType: "NatureOS.Event.Created",
            dataVersion: "1.0",
            data: mycorrhizaeEvent);

        await _eventGridClient.SendEventAsync(eventGridEvent);
        
        _logger.LogInformation("Published event {EventId} to Event Grid", mycorrhizaeEvent.EventId);
    }

    private async Task SendToProcessingQueueAsync(MycorrhizaeEvent mycorrhizaeEvent)
    {
        var sender = _serviceBusClient.CreateSender("mycorrhizae-events");
        var message = new ServiceBusMessage(JsonSerializer.Serialize(mycorrhizaeEvent))
        {
            MessageId = mycorrhizaeEvent.EventId,
            Subject = mycorrhizaeEvent.KingdomDomain,
            ContentType = "application/json"
        };

        await sender.SendMessageAsync(message);
        
        _logger.LogInformation("Sent event {EventId} to processing queue", mycorrhizaeEvent.EventId);
    }

    private async Task ProcessMycorrhizaeEventAsync(MycorrhizaeEvent mycorrhizaeEvent)
    {
        // Generate event ID if not provided
        if (string.IsNullOrEmpty(mycorrhizaeEvent.EventId))
        {
            mycorrhizaeEvent.EventId = Ulid.NewUlid().ToString();
        }

        // Set metadata
        if (mycorrhizaeEvent.Metadata == null)
        {
            mycorrhizaeEvent.Metadata = new EventMetadata();
        }
        mycorrhizaeEvent.Metadata.IngestedAt = DateTime.UtcNow;
        mycorrhizaeEvent.Metadata.PipelineVersion = "1.0.0";

        // Store and publish
        await StoreEventAsync(mycorrhizaeEvent);
        await PublishEventAsync(mycorrhizaeEvent);
        await SendToProcessingQueueAsync(mycorrhizaeEvent);
    }

    private async Task EnrichEventAsync(MycorrhizaeEvent mycorrhizaeEvent)
    {
        // Placeholder for semantic enrichment logic
        // This would typically involve:
        // 1. NLP processing of signal data
        // 2. Ontological classification
        // 3. ML-based signal decoding
        // 4. Taxonomic identification

        if (mycorrhizaeEvent.DecodedMeaning == null)
        {
            mycorrhizaeEvent.DecodedMeaning = new DecodedMeaning
            {
                Context = "https://schema.org/",
                Type = "BiologicalEvent",
                Algorithm = "basic-classifier-v1.0",
                Confidence = 0.5
            };
        }

        // Update the event in MINDEX
        await StoreEventAsync(mycorrhizaeEvent);
        
        _logger.LogInformation("Enriched event {EventId}", mycorrhizaeEvent.EventId);
    }
}

/// <summary>
/// IoT message format from devices
/// </summary>
public class IoTMessage
{
    public string DeviceId { get; set; } = string.Empty;
    public string? DeviceType { get; set; }
    public string? TenantId { get; set; }
    public DateTime Timestamp { get; set; }
    public object? Telemetry { get; set; }
    public GeoLocation? Location { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
} 