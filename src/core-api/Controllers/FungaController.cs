using Microsoft.AspNetCore.Mvc;
using NatureOS.CoreApi.Services;
using NatureOS.MINDEX.Models;

namespace NatureOS.CoreApi.Controllers;

/// <summary>
/// API controller for FUNGA (mycology) domain operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class FungaController : ControllerBase
{
    private readonly IFungaService _fungaService;
    private readonly ILogger<FungaController> _logger;

    public FungaController(IFungaService fungaService, ILogger<FungaController> logger)
    {
        _fungaService = fungaService;
        _logger = logger;
    }

    /// <summary>
    /// Get FUNGA events with advanced filtering
    /// </summary>
    /// <param name="sourceDevice">Filter by source device</param>
    /// <param name="phylum">Filter by fungal phylum</param>
    /// <param name="substrateType">Filter by substrate type</param>
    /// <param name="tempMin">Minimum temperature</param>
    /// <param name="tempMax">Maximum temperature</param>
    /// <param name="humidityMin">Minimum humidity</param>
    /// <param name="humidityMax">Maximum humidity</param>
    /// <param name="pHMin">Minimum pH</param>
    /// <param name="pHMax">Maximum pH</param>
    /// <param name="mycorrhizalOnly">Include only mycorrhizal fungi</param>
    /// <param name="startTime">Start time filter</param>
    /// <param name="endTime">End time filter</param>
    /// <param name="pageSize">Page size</param>
    /// <param name="continuationToken">Continuation token</param>
    /// <param name="sortOrder">Sort order</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paged result of FUNGA events</returns>
    [HttpGet("events")]
    [ProducesResponseType(typeof(PagedResult<MycorrhizaeEvent>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<PagedResult<MycorrhizaeEvent>>> GetFungaEvents(
        [FromQuery] string? sourceDevice = null,
        [FromQuery] string? phylum = null,
        [FromQuery] string? substrateType = null,
        [FromQuery] double? tempMin = null,
        [FromQuery] double? tempMax = null,
        [FromQuery] double? humidityMin = null,
        [FromQuery] double? humidityMax = null,
        [FromQuery] double? pHMin = null,
        [FromQuery] double? pHMax = null,
        [FromQuery] bool? mycorrhizalOnly = null,
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? continuationToken = null,
        [FromQuery] string sortOrder = "timestamp_desc",
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (pageSize <= 0 || pageSize > 1000)
            {
                return BadRequest("Page size must be between 1 and 1000");
            }

            var query = new FungaQuery
            {
                SourceDevice = sourceDevice,
                Phylum = phylum,
                SubstrateType = substrateType,
                MycorrhizalOnly = mycorrhizalOnly,
                StartTime = startTime,
                EndTime = endTime,
                PageSize = pageSize,
                ContinuationToken = continuationToken,
                SortOrder = sortOrder
            };

            // Set temperature range
            if (tempMin.HasValue || tempMax.HasValue)
            {
                query.TemperatureRange = new TemperatureRange
                {
                    Min = tempMin ?? double.MinValue,
                    Max = tempMax ?? double.MaxValue
                };
            }

            // Set humidity range
            if (humidityMin.HasValue || humidityMax.HasValue)
            {
                query.HumidityRange = new HumidityRange
                {
                    Min = humidityMin ?? 0,
                    Max = humidityMax ?? 100
                };
            }

            // Set pH range
            if (pHMin.HasValue || pHMax.HasValue)
            {
                query.pHRange = new pHRange
                {
                    Min = pHMin ?? 0,
                    Max = pHMax ?? 14
                };
            }

            var result = await _fungaService.GetFungaEventsAsync(query, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get FUNGA events");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Classify a fungal specimen from sensor data
    /// </summary>
    /// <param name="classificationRequest">Classification request with signal data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Classification result</returns>
    [HttpPost("classify")]
    [ProducesResponseType(typeof(FungalClassification), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<FungalClassification>> ClassifySpecimen(
        [FromBody] ClassificationRequest classificationRequest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (classificationRequest.SignalVector == null)
            {
                return BadRequest("Signal vector is required");
            }

            var classification = await _fungaService.ClassifySpecimenAsync(
                classificationRequest.SignalVector, cancellationToken);
            
            return Ok(classification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to classify fungal specimen");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Analyze mycorrhizal network within a geographic area
    /// </summary>
    /// <param name="latitude">Center latitude</param>
    /// <param name="longitude">Center longitude</param>
    /// <param name="radiusMeters">Radius in meters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Network analysis result</returns>
    [HttpGet("network/analyze")]
    [ProducesResponseType(typeof(MycorrhizalNetwork), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<MycorrhizalNetwork>> AnalyzeNetwork(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double radiusMeters = 1000,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (latitude < -90 || latitude > 90)
            {
                return BadRequest("Latitude must be between -90 and 90");
            }

            if (longitude < -180 || longitude > 180)
            {
                return BadRequest("Longitude must be between -180 and 180");
            }

            if (radiusMeters <= 0 || radiusMeters > 100000)
            {
                return BadRequest("Radius must be between 1 and 100000 meters");
            }

            var location = new GeoLocation
            {
                Latitude = latitude,
                Longitude = longitude
            };

            var network = await _fungaService.AnalyzeNetworkAsync(radiusMeters, location, cancellationToken);
            return Ok(network);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze mycorrhizal network");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get spore dispersal patterns for a time period and location
    /// </summary>
    /// <param name="latitude">Center latitude</param>
    /// <param name="longitude">Center longitude</param>
    /// <param name="startTime">Start time</param>
    /// <param name="endTime">End time</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Spore dispersal analysis</returns>
    [HttpGet("spores/dispersal")]
    [ProducesResponseType(typeof(SporeDispersal), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<SporeDispersal>> GetSporeDispersal(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] DateTime startTime,
        [FromQuery] DateTime endTime,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (latitude < -90 || latitude > 90)
            {
                return BadRequest("Latitude must be between -90 and 90");
            }

            if (longitude < -180 || longitude > 180)
            {
                return BadRequest("Longitude must be between -180 and 180");
            }

            if (startTime >= endTime)
            {
                return BadRequest("Start time must be before end time");
            }

            var location = new GeoLocation
            {
                Latitude = latitude,
                Longitude = longitude
            };

            var timeRange = new DateTimeRange
            {
                Start = startTime,
                End = endTime
            };

            var dispersal = await _fungaService.GetSporeDispersalAsync(timeRange, location, cancellationToken);
            return Ok(dispersal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get spore dispersal patterns");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get biodiversity metrics for a dataset
    /// </summary>
    /// <param name="sourceDevice">Filter by source device</param>
    /// <param name="phylum">Filter by phylum</param>
    /// <param name="substrateType">Filter by substrate type</param>
    /// <param name="startTime">Start time filter</param>
    /// <param name="endTime">End time filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Biodiversity metrics</returns>
    [HttpGet("biodiversity")]
    [ProducesResponseType(typeof(BiodiversityMetrics), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<BiodiversityMetrics>> GetBiodiversityMetrics(
        [FromQuery] string? sourceDevice = null,
        [FromQuery] string? phylum = null,
        [FromQuery] string? substrateType = null,
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new FungaQuery
            {
                SourceDevice = sourceDevice,
                Phylum = phylum,
                SubstrateType = substrateType,
                StartTime = startTime,
                EndTime = endTime,
                PageSize = 10000 // Large page size for diversity calculations
            };

            var metrics = await _fungaService.GetDiversityMetricsAsync(query, cancellationToken);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get biodiversity metrics");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get species abundance data
    /// </summary>
    /// <param name="genus">Filter by genus</param>
    /// <param name="habitat">Filter by habitat</param>
    /// <param name="startTime">Start time filter</param>
    /// <param name="endTime">End time filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Species abundance data</returns>
    [HttpGet("abundance")]
    [ProducesResponseType(typeof(SpeciesAbundance), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<SpeciesAbundance>> GetSpeciesAbundance(
        [FromQuery] string? genus = null,
        [FromQuery] string? habitat = null,
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new FungaQuery
            {
                StartTime = startTime,
                EndTime = endTime,
                PageSize = 10000
            };

            var events = await _fungaService.GetFungaEventsAsync(query, cancellationToken);
            
            // Filter by genus if specified
            var filteredEvents = events.Items;
            if (!string.IsNullOrEmpty(genus))
            {
                filteredEvents = filteredEvents.Where(e => 
                    e.References?.Taxonomy?.Genus?.Equals(genus, StringComparison.OrdinalIgnoreCase) == true);
            }

            // Filter by habitat if specified
            if (!string.IsNullOrEmpty(habitat))
            {
                filteredEvents = filteredEvents.Where(e => 
                    e.References?.Environment?.Habitat?.Equals(habitat, StringComparison.OrdinalIgnoreCase) == true);
            }

            var speciesCounts = filteredEvents
                .GroupBy(e => e.References?.Taxonomy?.ScientificName ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());

            var abundance = new SpeciesAbundance
            {
                TotalObservations = filteredEvents.Count(),
                SpeciesCounts = speciesCounts,
                DominantSpecies = speciesCounts.OrderByDescending(kvp => kvp.Value).Take(10).ToList(),
                RareSpecies = speciesCounts.Where(kvp => kvp.Value <= 2).ToList()
            };

            return Ok(abundance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get species abundance");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get environmental preferences for a species
    /// </summary>
    /// <param name="scientificName">Scientific name of the species</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Environmental preferences</returns>
    [HttpGet("species/{scientificName}/preferences")]
    [ProducesResponseType(typeof(EnvironmentalPreferences), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<EnvironmentalPreferences>> GetEnvironmentalPreferences(
        string scientificName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new FungaQuery
            {
                PageSize = 10000
            };

            var events = await _fungaService.GetFungaEventsAsync(query, cancellationToken);
            
            var speciesEvents = events.Items.Where(e => 
                e.References?.Taxonomy?.ScientificName?.Equals(scientificName, StringComparison.OrdinalIgnoreCase) == true);

            if (!speciesEvents.Any())
            {
                return NotFound($"No data found for species: {scientificName}");
            }

            var temperatures = speciesEvents
                .Where(e => e.References?.Environment?.Temperature.HasValue == true)
                .Select(e => e.References!.Environment!.Temperature!.Value)
                .ToList();

            var humidities = speciesEvents
                .Where(e => e.References?.Environment?.Humidity.HasValue == true)
                .Select(e => e.References!.Environment!.Humidity!.Value)
                .ToList();

            var pHValues = speciesEvents
                .Where(e => e.References?.Environment?.pH.HasValue == true)
                .Select(e => e.References!.Environment!.pH!.Value)
                .ToList();

            var preferences = new EnvironmentalPreferences
            {
                SpeciesName = scientificName,
                TemperatureRange = temperatures.Any() ? new TemperatureRange
                {
                    Min = temperatures.Min(),
                    Max = temperatures.Max()
                } : null,
                HumidityRange = humidities.Any() ? new HumidityRange
                {
                    Min = humidities.Min(),
                    Max = humidities.Max()
                } : null,
                pHRange = pHValues.Any() ? new pHRange
                {
                    Min = pHValues.Min(),
                    Max = pHValues.Max()
                } : null,
                PreferredHabitats = speciesEvents
                    .Where(e => !string.IsNullOrEmpty(e.References?.Environment?.Habitat))
                    .GroupBy(e => e.References!.Environment!.Habitat!)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => g.Key)
                    .ToList(),
                PreferredSubstrates = speciesEvents
                    .Where(e => !string.IsNullOrEmpty(e.References?.Environment?.Substrate))
                    .GroupBy(e => e.References!.Environment!.Substrate!)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => g.Key)
                    .ToList()
            };

            return Ok(preferences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get environmental preferences for {SpeciesName}", scientificName);
            return StatusCode(500, "Internal server error");
        }
    }
}

/// <summary>
/// Request model for fungal classification
/// </summary>
public class ClassificationRequest
{
    /// <summary>
    /// Sensor signal vector data
    /// </summary>
    public object? SignalVector { get; set; }

    /// <summary>
    /// Optional context information
    /// </summary>
    public string? Context { get; set; }
}

/// <summary>
/// Species abundance data
/// </summary>
public class SpeciesAbundance
{
    /// <summary>
    /// Total number of observations
    /// </summary>
    public int TotalObservations { get; set; }

    /// <summary>
    /// Count of observations per species
    /// </summary>
    public Dictionary<string, int> SpeciesCounts { get; set; } = new();

    /// <summary>
    /// Top 10 most abundant species
    /// </summary>
    public List<KeyValuePair<string, int>> DominantSpecies { get; set; } = new();

    /// <summary>
    /// Species with 2 or fewer observations
    /// </summary>
    public List<KeyValuePair<string, int>> RareSpecies { get; set; } = new();
}

/// <summary>
/// Environmental preferences for a species
/// </summary>
public class EnvironmentalPreferences
{
    /// <summary>
    /// Species name
    /// </summary>
    public string SpeciesName { get; set; } = string.Empty;

    /// <summary>
    /// Preferred temperature range
    /// </summary>
    public TemperatureRange? TemperatureRange { get; set; }

    /// <summary>
    /// Preferred humidity range
    /// </summary>
    public HumidityRange? HumidityRange { get; set; }

    /// <summary>
    /// Preferred pH range
    /// </summary>
    public pHRange? pHRange { get; set; }

    /// <summary>
    /// Preferred habitats
    /// </summary>
    public List<string> PreferredHabitats { get; set; } = new();

    /// <summary>
    /// Preferred substrates
    /// </summary>
    public List<string> PreferredSubstrates { get; set; } = new();
} 