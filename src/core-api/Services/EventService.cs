using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using NatureOS.MINDEX.Models;
using System.Net;

namespace NatureOS.CoreApi.Services;

/// <summary>
/// Implementation of IEventService for managing Mycorrhizae Protocol events
/// </summary>
public class EventService : IEventService
{
    private readonly Container _eventsContainer;
    private readonly ILogger<EventService> _logger;

    public EventService(CosmosClient cosmosClient, ILogger<EventService> logger)
    {
        _logger = logger;
        var database = cosmosClient.GetDatabase("mindex");
        _eventsContainer = database.GetContainer("events");
    }

    public async Task<MycorrhizaeEvent> CreateEventAsync(MycorrhizaeEvent mycorrhizaeEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate ULID if not provided
            if (string.IsNullOrEmpty(mycorrhizaeEvent.EventId))
            {
                mycorrhizaeEvent.EventId = Ulid.NewUlid().ToString();
            }

            // Set ingestion metadata
            if (mycorrhizaeEvent.Metadata == null)
            {
                mycorrhizaeEvent.Metadata = new EventMetadata();
            }
            mycorrhizaeEvent.Metadata.IngestedAt = DateTime.UtcNow;

            // Create the item in Cosmos DB
            var response = await _eventsContainer.CreateItemAsync(
                mycorrhizaeEvent,
                new PartitionKey(mycorrhizaeEvent.SourceDevice),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Created event {EventId} from device {SourceDevice}", 
                mycorrhizaeEvent.EventId, mycorrhizaeEvent.SourceDevice);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogWarning("Event {EventId} already exists", mycorrhizaeEvent.EventId);
            throw new InvalidOperationException($"Event {mycorrhizaeEvent.EventId} already exists", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create event {EventId}", mycorrhizaeEvent.EventId);
            throw;
        }
    }

    public async Task<MycorrhizaeEvent?> GetEventAsync(string eventId, string sourceDevice, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _eventsContainer.ReadItemAsync<MycorrhizaeEvent>(
                eventId,
                new PartitionKey(sourceDevice),
                cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get event {EventId} from device {SourceDevice}", eventId, sourceDevice);
            throw;
        }
    }

    public async Task<PagedResult<MycorrhizaeEvent>> QueryEventsAsync(EventQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var queryDefinition = BuildQuery(query);
            var requestOptions = new QueryRequestOptions
            {
                MaxItemCount = query.PageSize
            };

            var iterator = _eventsContainer.GetItemQueryIterator<MycorrhizaeEvent>(
                queryDefinition,
                continuationToken: query.ContinuationToken,
                requestOptions: requestOptions);

            var results = new List<MycorrhizaeEvent>();
            string? continuationToken = null;

            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                results.AddRange(response);
                continuationToken = response.ContinuationToken;
            }

            return new PagedResult<MycorrhizaeEvent>
            {
                Items = results,
                ContinuationToken = continuationToken,
                HasMore = !string.IsNullOrEmpty(continuationToken)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query events");
            throw;
        }
    }

    public async Task<PagedResult<MycorrhizaeEvent>> GetEventsAsync(EventQuery query, CancellationToken cancellationToken = default)
    {
        // Alias for QueryEventsAsync to maintain compatibility
        return await QueryEventsAsync(query, cancellationToken);
    }

    public async Task<IEnumerable<MycorrhizaeEvent>> GetEventsByDeviceAsync(string sourceDevice, int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.source_device = @sourceDevice ORDER BY c.timestamp DESC OFFSET 0 LIMIT @limit")
                .WithParameter("@sourceDevice", sourceDevice)
                .WithParameter("@limit", limit);

            var iterator = _eventsContainer.GetItemQueryIterator<MycorrhizaeEvent>(
                query,
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(sourceDevice) });

            var results = new List<MycorrhizaeEvent>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                results.AddRange(response);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get events for device {SourceDevice}", sourceDevice);
            throw;
        }
    }

    public async Task<IEnumerable<MycorrhizaeEvent>> GetEventsByDomainAsync(string kingdomDomain, int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.kingdom_domain = @kingdomDomain ORDER BY c.timestamp DESC OFFSET 0 LIMIT @limit")
                .WithParameter("@kingdomDomain", kingdomDomain)
                .WithParameter("@limit", limit);

            var iterator = _eventsContainer.GetItemQueryIterator<MycorrhizaeEvent>(query);
            var results = new List<MycorrhizaeEvent>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                results.AddRange(response);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get events for domain {KingdomDomain}", kingdomDomain);
            throw;
        }
    }

    public async Task<IEnumerable<MycorrhizaeEvent>> GetEventsByTimeRangeAsync(DateTime startTime, DateTime endTime, int limit = 1000, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.timestamp >= @startTime AND c.timestamp <= @endTime ORDER BY c.timestamp DESC OFFSET 0 LIMIT @limit")
                .WithParameter("@startTime", startTime)
                .WithParameter("@endTime", endTime)
                .WithParameter("@limit", limit);

            var iterator = _eventsContainer.GetItemQueryIterator<MycorrhizaeEvent>(query);
            var results = new List<MycorrhizaeEvent>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                results.AddRange(response);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get events for time range {StartTime} to {EndTime}", startTime, endTime);
            throw;
        }
    }

    public async Task<bool> DeleteEventAsync(string eventId, string sourceDevice, CancellationToken cancellationToken = default)
    {
        try
        {
            await _eventsContainer.DeleteItemAsync<MycorrhizaeEvent>(
                eventId,
                new PartitionKey(sourceDevice),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Deleted event {EventId} from device {SourceDevice}", eventId, sourceDevice);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete event {EventId} from device {SourceDevice}", eventId, sourceDevice);
            throw;
        }
    }

    public async Task<EventStatistics> GetEventStatisticsAsync(EventQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var statistics = new EventStatistics();

            // Get total count
            var countQuery = new QueryDefinition("SELECT VALUE COUNT(1) FROM c");
            var countIterator = _eventsContainer.GetItemQueryIterator<long>(countQuery);
            if (countIterator.HasMoreResults)
            {
                var countResponse = await countIterator.ReadNextAsync(cancellationToken);
                statistics.TotalEvents = countResponse.FirstOrDefault();
            }

            // Get events by domain
            var domainQuery = new QueryDefinition(
                "SELECT c.kingdom_domain, COUNT(1) as count FROM c GROUP BY c.kingdom_domain");
            var domainIterator = _eventsContainer.GetItemQueryIterator<dynamic>(domainQuery);
            while (domainIterator.HasMoreResults)
            {
                var domainResponse = await domainIterator.ReadNextAsync(cancellationToken);
                foreach (var item in domainResponse)
                {
                    statistics.EventsByDomain[item.kingdom_domain] = item.count;
                }
            }

            // Get events by device
            var deviceQuery = new QueryDefinition(
                "SELECT c.source_device, COUNT(1) as count FROM c GROUP BY c.source_device");
            var deviceIterator = _eventsContainer.GetItemQueryIterator<dynamic>(deviceQuery);
            while (deviceIterator.HasMoreResults)
            {
                var deviceResponse = await deviceIterator.ReadNextAsync(cancellationToken);
                foreach (var item in deviceResponse)
                {
                    statistics.EventsByDevice[item.source_device] = item.count;
                }
            }

            // Get time range
            var timeRangeQuery = new QueryDefinition(
                "SELECT MIN(c.timestamp) as min_time, MAX(c.timestamp) as max_time FROM c");
            var timeIterator = _eventsContainer.GetItemQueryIterator<dynamic>(timeRangeQuery);
            if (timeIterator.HasMoreResults)
            {
                var timeResponse = await timeIterator.ReadNextAsync(cancellationToken);
                var timeData = timeResponse.FirstOrDefault();
                if (timeData != null)
                {
                    statistics.TimeRange = new DateTimeRange
                    {
                        Start = timeData.min_time,
                        End = timeData.max_time
                    };
                }
            }

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get event statistics");
            throw;
        }
    }

    private static QueryDefinition BuildQuery(EventQuery query)
    {
        var conditions = new List<string>();
        var parameters = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(query.SourceDevice))
        {
            conditions.Add("c.source_device = @sourceDevice");
            parameters["@sourceDevice"] = query.SourceDevice;
        }

        if (!string.IsNullOrEmpty(query.KingdomDomain))
        {
            conditions.Add("c.kingdom_domain = @kingdomDomain");
            parameters["@kingdomDomain"] = query.KingdomDomain;
        }

        if (query.StartTime.HasValue)
        {
            conditions.Add("c.timestamp >= @startTime");
            parameters["@startTime"] = query.StartTime.Value;
        }

        if (query.EndTime.HasValue)
        {
            conditions.Add("c.timestamp <= @endTime");
            parameters["@endTime"] = query.EndTime.Value;
        }

        if (!string.IsNullOrEmpty(query.TenantId))
        {
            conditions.Add("c.metadata.tenant_id = @tenantId");
            parameters["@tenantId"] = query.TenantId;
        }

        var whereClause = conditions.Any() ? $"WHERE {string.Join(" AND ", conditions)}" : "";
        var orderClause = query.SortOrder == "timestamp_asc" ? "ORDER BY c.timestamp ASC" : "ORDER BY c.timestamp DESC";

        var sql = $"SELECT * FROM c {whereClause} {orderClause}";
        
        var queryDefinition = new QueryDefinition(sql);
        foreach (var param in parameters)
        {
            queryDefinition = queryDefinition.WithParameter(param.Key, param.Value);
        }

        return queryDefinition;
    }
} 