using NatureOS.MINDEX.Models;

namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service interface for managing Mycorrhizae Protocol events
/// </summary>
public interface IEventService
{
    /// <summary>
    /// Store a new event in MINDEX
    /// </summary>
    /// <param name="mycorrhizaeEvent">The event to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The stored event with assigned ID</returns>
    Task<MycorrhizaeEvent> CreateEventAsync(MycorrhizaeEvent mycorrhizaeEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve an event by ID
    /// </summary>
    /// <param name="eventId">Event ID</param>
    /// <param name="sourceDevice">Source device partition key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The event if found</returns>
    Task<MycorrhizaeEvent?> GetEventAsync(string eventId, string sourceDevice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Query events with filtering and pagination
    /// </summary>
    /// <param name="query">Query parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paged result of events</returns>
    Task<PagedResult<MycorrhizaeEvent>> QueryEventsAsync(EventQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get events by source device
    /// </summary>
    /// <param name="sourceDevice">Source device ID</param>
    /// <param name="limit">Maximum number of events to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of events from the device</returns>
    Task<IEnumerable<MycorrhizaeEvent>> GetEventsByDeviceAsync(string sourceDevice, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get events by kingdom/domain
    /// </summary>
    /// <param name="kingdomDomain">Kingdom domain filter</param>
    /// <param name="limit">Maximum number of events to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of events in the domain</returns>
    Task<IEnumerable<MycorrhizaeEvent>> GetEventsByDomainAsync(string kingdomDomain, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get events within a time range
    /// </summary>
    /// <param name="startTime">Start time (inclusive)</param>
    /// <param name="endTime">End time (inclusive)</param>
    /// <param name="limit">Maximum number of events to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of events in the time range</returns>
    Task<IEnumerable<MycorrhizaeEvent>> GetEventsByTimeRangeAsync(DateTime startTime, DateTime endTime, int limit = 1000, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete an event
    /// </summary>
    /// <param name="eventId">Event ID to delete</param>
    /// <param name="sourceDevice">Source device partition key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> DeleteEventAsync(string eventId, string sourceDevice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get aggregated statistics for events
    /// </summary>
    /// <param name="query">Query parameters for aggregation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Event statistics</returns>
    Task<EventStatistics> GetEventStatisticsAsync(EventQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Query parameters for event searches
/// </summary>
public class EventQuery
{
    /// <summary>
    /// Source device filter
    /// </summary>
    public string? SourceDevice { get; set; }

    /// <summary>
    /// Kingdom domain filter
    /// </summary>
    public string? KingdomDomain { get; set; }

    /// <summary>
    /// Start time filter
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// End time filter
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Tenant ID filter
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Page size for pagination
    /// </summary>
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// Continuation token for pagination
    /// </summary>
    public string? ContinuationToken { get; set; }

    /// <summary>
    /// Sort order (timestamp_asc, timestamp_desc)
    /// </summary>
    public string SortOrder { get; set; } = "timestamp_desc";

    /// <summary>
    /// Include decoded meaning in results
    /// </summary>
    public bool IncludeDecodedMeaning { get; set; } = true;

    /// <summary>
    /// Include metadata in results
    /// </summary>
    public bool IncludeMetadata { get; set; } = false;
}

/// <summary>
/// Paged result container
/// </summary>
/// <typeparam name="T">Type of items in the result</typeparam>
public class PagedResult<T>
{
    /// <summary>
    /// Items in this page
    /// </summary>
    public IEnumerable<T> Items { get; set; } = [];

    /// <summary>
    /// Total count (if available)
    /// </summary>
    public long? TotalCount { get; set; }

    /// <summary>
    /// Continuation token for next page
    /// </summary>
    public string? ContinuationToken { get; set; }

    /// <summary>
    /// Whether there are more pages
    /// </summary>
    public bool HasMore { get; set; }
}

/// <summary>
/// Event statistics and aggregations
/// </summary>
public class EventStatistics
{
    /// <summary>
    /// Total event count
    /// </summary>
    public long TotalEvents { get; set; }

    /// <summary>
    /// Events by kingdom/domain
    /// </summary>
    public Dictionary<string, long> EventsByDomain { get; set; } = new();

    /// <summary>
    /// Events by source device
    /// </summary>
    public Dictionary<string, long> EventsByDevice { get; set; } = new();

    /// <summary>
    /// Time range of events
    /// </summary>
    public DateTimeRange? TimeRange { get; set; }

    /// <summary>
    /// Data quality statistics
    /// </summary>
    public QualityStatistics? Quality { get; set; }
}

/// <summary>
/// Date time range
/// </summary>
public class DateTimeRange
{
    /// <summary>
    /// Start time
    /// </summary>
    public DateTime Start { get; set; }

    /// <summary>
    /// End time
    /// </summary>
    public DateTime End { get; set; }
}

/// <summary>
/// Data quality statistics
/// </summary>
public class QualityStatistics
{
    /// <summary>
    /// Average quality score
    /// </summary>
    public double AverageQualityScore { get; set; }

    /// <summary>
    /// Events with decoded meaning
    /// </summary>
    public long EventsWithDecodedMeaning { get; set; }

    /// <summary>
    /// Events with location data
    /// </summary>
    public long EventsWithLocation { get; set; }

    /// <summary>
    /// Events with taxonomic classification
    /// </summary>
    public long EventsWithTaxonomy { get; set; }
} 