using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NatureOS.CoreApi.Services;
using NatureOS.MINDEX.Models;

namespace NatureOS.CoreApi.Controllers;

/// <summary>
/// API controller for managing Mycorrhizae Protocol events
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;
    private readonly ILogger<EventsController> _logger;

    public EventsController(IEventService eventService, ILogger<EventsController> logger)
    {
        _eventService = eventService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new Mycorrhizae Protocol event
    /// </summary>
    /// <param name="mycorrhizaeEvent">The event to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created event</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MycorrhizaeEvent), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<MycorrhizaeEvent>> CreateEvent(
        [FromBody] MycorrhizaeEvent mycorrhizaeEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate required fields
            if (string.IsNullOrEmpty(mycorrhizaeEvent.SourceDevice))
            {
                return BadRequest("SourceDevice is required");
            }

            if (string.IsNullOrEmpty(mycorrhizaeEvent.KingdomDomain))
            {
                return BadRequest("KingdomDomain is required");
            }

            var createdEvent = await _eventService.CreateEventAsync(mycorrhizaeEvent, cancellationToken);
            
            return CreatedAtAction(
                nameof(GetEvent),
                new { eventId = createdEvent.EventId, sourceDevice = createdEvent.SourceDevice },
                createdEvent);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create event");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get an event by ID
    /// </summary>
    /// <param name="eventId">Event ID</param>
    /// <param name="sourceDevice">Source device ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The event if found</returns>
    [HttpGet("{eventId}")]
    [ProducesResponseType(typeof(MycorrhizaeEvent), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<MycorrhizaeEvent>> GetEvent(
        string eventId,
        [FromQuery] string sourceDevice,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(sourceDevice))
            {
                return BadRequest("sourceDevice query parameter is required");
            }

            var mycorrhizaeEvent = await _eventService.GetEventAsync(eventId, sourceDevice, cancellationToken);
            
            if (mycorrhizaeEvent == null)
            {
                return NotFound($"Event {eventId} not found for device {sourceDevice}");
            }

            return Ok(mycorrhizaeEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get event {EventId}", eventId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Query events with filtering and pagination
    /// </summary>
    /// <param name="sourceDevice">Filter by source device</param>
    /// <param name="kingdomDomain">Filter by kingdom domain</param>
    /// <param name="startTime">Filter by start time</param>
    /// <param name="endTime">Filter by end time</param>
    /// <param name="tenantId">Filter by tenant ID</param>
    /// <param name="pageSize">Page size (default: 50, max: 1000)</param>
    /// <param name="continuationToken">Continuation token for pagination</param>
    /// <param name="sortOrder">Sort order (timestamp_asc or timestamp_desc)</param>
    /// <param name="includeDecodedMeaning">Include decoded meaning in results</param>
    /// <param name="includeMetadata">Include metadata in results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paged result of events</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<MycorrhizaeEvent>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<PagedResult<MycorrhizaeEvent>>> QueryEvents(
        [FromQuery] string? sourceDevice = null,
        [FromQuery] string? kingdomDomain = null,
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null,
        [FromQuery] string? tenantId = null,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? continuationToken = null,
        [FromQuery] string sortOrder = "timestamp_desc",
        [FromQuery] bool includeDecodedMeaning = true,
        [FromQuery] bool includeMetadata = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate page size
            if (pageSize <= 0 || pageSize > 1000)
            {
                return BadRequest("Page size must be between 1 and 1000");
            }

            // Validate sort order
            if (!new[] { "timestamp_asc", "timestamp_desc" }.Contains(sortOrder))
            {
                return BadRequest("Sort order must be 'timestamp_asc' or 'timestamp_desc'");
            }

            var query = new EventQuery
            {
                SourceDevice = sourceDevice,
                KingdomDomain = kingdomDomain,
                StartTime = startTime,
                EndTime = endTime,
                TenantId = tenantId,
                PageSize = pageSize,
                ContinuationToken = continuationToken,
                SortOrder = sortOrder,
                IncludeDecodedMeaning = includeDecodedMeaning,
                IncludeMetadata = includeMetadata
            };

            var result = await _eventService.QueryEventsAsync(query, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query events");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get events by source device
    /// </summary>
    /// <param name="sourceDevice">Source device ID</param>
    /// <param name="limit">Maximum number of events to return (default: 100, max: 1000)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of events from the device</returns>
    [HttpGet("by-device/{sourceDevice}")]
    [ProducesResponseType(typeof(IEnumerable<MycorrhizaeEvent>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<MycorrhizaeEvent>>> GetEventsByDevice(
        string sourceDevice,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (limit <= 0 || limit > 1000)
            {
                return BadRequest("Limit must be between 1 and 1000");
            }

            var events = await _eventService.GetEventsByDeviceAsync(sourceDevice, limit, cancellationToken);
            return Ok(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get events by device {SourceDevice}", sourceDevice);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get events by kingdom domain
    /// </summary>
    /// <param name="kingdomDomain">Kingdom domain (e.g., FUNGA.signaling)</param>
    /// <param name="limit">Maximum number of events to return (default: 100, max: 1000)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of events in the domain</returns>
    [HttpGet("by-domain/{kingdomDomain}")]
    [ProducesResponseType(typeof(IEnumerable<MycorrhizaeEvent>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<MycorrhizaeEvent>>> GetEventsByDomain(
        string kingdomDomain,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (limit <= 0 || limit > 1000)
            {
                return BadRequest("Limit must be between 1 and 1000");
            }

            var events = await _eventService.GetEventsByDomainAsync(kingdomDomain, limit, cancellationToken);
            return Ok(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get events by domain {KingdomDomain}", kingdomDomain);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get events within a time range
    /// </summary>
    /// <param name="startTime">Start time (inclusive)</param>
    /// <param name="endTime">End time (inclusive)</param>
    /// <param name="limit">Maximum number of events to return (default: 1000)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of events in the time range</returns>
    [HttpGet("by-time")]
    [ProducesResponseType(typeof(IEnumerable<MycorrhizaeEvent>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<MycorrhizaeEvent>>> GetEventsByTimeRange(
        [FromQuery] DateTime startTime,
        [FromQuery] DateTime endTime,
        [FromQuery] int limit = 1000,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (startTime >= endTime)
            {
                return BadRequest("Start time must be before end time");
            }

            if (limit <= 0 || limit > 10000)
            {
                return BadRequest("Limit must be between 1 and 10000");
            }

            var events = await _eventService.GetEventsByTimeRangeAsync(startTime, endTime, limit, cancellationToken);
            return Ok(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get events by time range {StartTime} to {EndTime}", startTime, endTime);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete an event
    /// </summary>
    /// <param name="eventId">Event ID to delete</param>
    /// <param name="sourceDevice">Source device ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpDelete("{eventId}")]
    [Authorize]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult> DeleteEvent(
        string eventId,
        [FromQuery] string sourceDevice,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(sourceDevice))
            {
                return BadRequest("sourceDevice query parameter is required");
            }

            var deleted = await _eventService.DeleteEventAsync(eventId, sourceDevice, cancellationToken);
            
            if (!deleted)
            {
                return NotFound($"Event {eventId} not found for device {sourceDevice}");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete event {EventId}", eventId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get event statistics and aggregations
    /// </summary>
    /// <param name="sourceDevice">Filter by source device</param>
    /// <param name="kingdomDomain">Filter by kingdom domain</param>
    /// <param name="startTime">Filter by start time</param>
    /// <param name="endTime">Filter by end time</param>
    /// <param name="tenantId">Filter by tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Event statistics</returns>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(EventStatistics), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<EventStatistics>> GetEventStatistics(
        [FromQuery] string? sourceDevice = null,
        [FromQuery] string? kingdomDomain = null,
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null,
        [FromQuery] string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new EventQuery
            {
                SourceDevice = sourceDevice,
                KingdomDomain = kingdomDomain,
                StartTime = startTime,
                EndTime = endTime,
                TenantId = tenantId
            };

            var statistics = await _eventService.GetEventStatisticsAsync(query, cancellationToken);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get event statistics");
            return StatusCode(500, "Internal server error");
        }
    }
} 