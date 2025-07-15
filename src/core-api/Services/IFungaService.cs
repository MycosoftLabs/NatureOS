using NatureOS.MINDEX.Models;

namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service interface for FUNGA (mycology) domain operations
/// </summary>
public interface IFungaService
{
    /// <summary>
    /// Get FUNGA events
    /// </summary>
    /// <param name="query">Query parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paged result of FUNGA events</returns>
    Task<PagedResult<MycorrhizaeEvent>> GetFungaEventsAsync(FungaQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Classify fungal specimen
    /// </summary>
    /// <param name="signalVector">Sensor data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Classification result</returns>
    Task<FungalClassification> ClassifySpecimenAsync(object signalVector, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get mycorrhizal network analysis
    /// </summary>
    /// <param name="locationRadius">Geographic radius in meters</param>
    /// <param name="location">Center location</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Network analysis result</returns>
    Task<MycorrhizalNetwork> AnalyzeNetworkAsync(double locationRadius, GeoLocation location, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get spore dispersal patterns
    /// </summary>
    /// <param name="timeRange">Time range for analysis</param>
    /// <param name="location">Location for analysis</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dispersal pattern analysis</returns>
    Task<SporeDispersal> GetSporeDispersalAsync(DateTimeRange timeRange, GeoLocation location, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get fungal diversity metrics
    /// </summary>
    /// <param name="query">Query parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Diversity metrics</returns>
    Task<BiodiversityMetrics> GetDiversityMetricsAsync(FungaQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// FUNGA-specific query parameters
/// </summary>
public class FungaQuery : EventQuery
{
    /// <summary>
    /// Fungal phylum filter
    /// </summary>
    public string? Phylum { get; set; }

    /// <summary>
    /// Substrate type filter
    /// </summary>
    public string? SubstrateType { get; set; }

    /// <summary>
    /// Temperature range filter
    /// </summary>
    public TemperatureRange? TemperatureRange { get; set; }

    /// <summary>
    /// Humidity range filter
    /// </summary>
    public HumidityRange? HumidityRange { get; set; }

    /// <summary>
    /// pH range filter
    /// </summary>
    public pHRange? pHRange { get; set; }

    /// <summary>
    /// Include only mycorrhizal fungi
    /// </summary>
    public bool? MycorrhizalOnly { get; set; }
}

/// <summary>
/// Fungal classification result
/// </summary>
public class FungalClassification
{
    /// <summary>
    /// Predicted taxonomy
    /// </summary>
    public TaxonomicClassification Taxonomy { get; set; } = new();

    /// <summary>
    /// Classification confidence
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Alternative classifications
    /// </summary>
    public List<AlternativeClassification> Alternatives { get; set; } = new();

    /// <summary>
    /// Morphological features detected
    /// </summary>
    public MorphologicalFeatures? Features { get; set; }

    /// <summary>
    /// Ecological indicators
    /// </summary>
    public EcologicalIndicators? Ecology { get; set; }
}

/// <summary>
/// Alternative classification option
/// </summary>
public class AlternativeClassification
{
    /// <summary>
    /// Alternative taxonomy
    /// </summary>
    public TaxonomicClassification Taxonomy { get; set; } = new();

    /// <summary>
    /// Confidence score
    /// </summary>
    public double Confidence { get; set; }
}

/// <summary>
/// Morphological features
/// </summary>
public class MorphologicalFeatures
{
    /// <summary>
    /// Cap diameter (mm)
    /// </summary>
    public double? CapDiameter { get; set; }

    /// <summary>
    /// Stem height (mm)
    /// </summary>
    public double? StemHeight { get; set; }

    /// <summary>
    /// Spore size (micrometers)
    /// </summary>
    public double? SporeSize { get; set; }

    /// <summary>
    /// Color description
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Texture description
    /// </summary>
    public string? Texture { get; set; }
}

/// <summary>
/// Ecological indicators
/// </summary>
public class EcologicalIndicators
{
    /// <summary>
    /// Mycorrhizal type
    /// </summary>
    public string? MycorrhizalType { get; set; }

    /// <summary>
    /// Host tree species
    /// </summary>
    public List<string>? HostSpecies { get; set; }

    /// <summary>
    /// Soil health indicators
    /// </summary>
    public Dictionary<string, double>? SoilHealth { get; set; }

    /// <summary>
    /// Ecosystem role
    /// </summary>
    public string? EcosystemRole { get; set; }
}

/// <summary>
/// Mycorrhizal network analysis
/// </summary>
public class MycorrhizalNetwork
{
    /// <summary>
    /// Network nodes (fungi and plants)
    /// </summary>
    public List<NetworkNode> Nodes { get; set; } = new();

    /// <summary>
    /// Network connections
    /// </summary>
    public List<NetworkEdge> Edges { get; set; } = new();

    /// <summary>
    /// Network metrics
    /// </summary>
    public NetworkMetrics Metrics { get; set; } = new();
}

/// <summary>
/// Network node
/// </summary>
public class NetworkNode
{
    /// <summary>
    /// Node ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Node type (fungi/plant)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Species information
    /// </summary>
    public TaxonomicClassification? Species { get; set; }

    /// <summary>
    /// Location
    /// </summary>
    public GeoLocation Location { get; set; } = new();

    /// <summary>
    /// Connection count
    /// </summary>
    public int ConnectionCount { get; set; }
}

/// <summary>
/// Network edge (connection)
/// </summary>
public class NetworkEdge
{
    /// <summary>
    /// Source node ID
    /// </summary>
    public string SourceId { get; set; } = string.Empty;

    /// <summary>
    /// Target node ID
    /// </summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>
    /// Connection strength
    /// </summary>
    public double Strength { get; set; }

    /// <summary>
    /// Connection type
    /// </summary>
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Network metrics
/// </summary>
public class NetworkMetrics
{
    /// <summary>
    /// Total nodes
    /// </summary>
    public int NodeCount { get; set; }

    /// <summary>
    /// Total edges
    /// </summary>
    public int EdgeCount { get; set; }

    /// <summary>
    /// Network density
    /// </summary>
    public double Density { get; set; }

    /// <summary>
    /// Average clustering coefficient
    /// </summary>
    public double ClusteringCoefficient { get; set; }
}

/// <summary>
/// Spore dispersal analysis
/// </summary>
public class SporeDispersal
{
    /// <summary>
    /// Dispersal patterns
    /// </summary>
    public List<DispersalPattern> Patterns { get; set; } = new();

    /// <summary>
    /// Wind influence analysis
    /// </summary>
    public WindInfluence? WindInfluence { get; set; }

    /// <summary>
    /// Temporal patterns
    /// </summary>
    public TemporalPattern? TemporalPattern { get; set; }
}

/// <summary>
/// Dispersal pattern
/// </summary>
public class DispersalPattern
{
    /// <summary>
    /// Source location
    /// </summary>
    public GeoLocation Source { get; set; } = new();

    /// <summary>
    /// Target locations
    /// </summary>
    public List<GeoLocation> Targets { get; set; } = new();

    /// <summary>
    /// Dispersal distance (meters)
    /// </summary>
    public double Distance { get; set; }

    /// <summary>
    /// Spore concentration
    /// </summary>
    public double Concentration { get; set; }
}

/// <summary>
/// Wind influence on dispersal
/// </summary>
public class WindInfluence
{
    /// <summary>
    /// Wind direction (degrees)
    /// </summary>
    public double Direction { get; set; }

    /// <summary>
    /// Wind speed (m/s)
    /// </summary>
    public double Speed { get; set; }

    /// <summary>
    /// Correlation with dispersal
    /// </summary>
    public double Correlation { get; set; }
}

/// <summary>
/// Temporal dispersal pattern
/// </summary>
public class TemporalPattern
{
    /// <summary>
    /// Peak dispersal times
    /// </summary>
    public List<TimeSpan> PeakTimes { get; set; } = new();

    /// <summary>
    /// Seasonal variations
    /// </summary>
    public Dictionary<string, double> SeasonalVariations { get; set; } = new();
}

/// <summary>
/// Biodiversity metrics
/// </summary>
public class BiodiversityMetrics
{
    /// <summary>
    /// Species richness
    /// </summary>
    public int SpeciesRichness { get; set; }

    /// <summary>
    /// Shannon diversity index
    /// </summary>
    public double ShannonIndex { get; set; }

    /// <summary>
    /// Simpson diversity index
    /// </summary>
    public double SimpsonIndex { get; set; }

    /// <summary>
    /// Evenness index
    /// </summary>
    public double EvennessIndex { get; set; }

    /// <summary>
    /// Rare species count
    /// </summary>
    public int RareSpeciesCount { get; set; }
}

/// <summary>
/// Temperature range
/// </summary>
public class TemperatureRange
{
    public double Min { get; set; }
    public double Max { get; set; }
}

/// <summary>
/// Humidity range
/// </summary>
public class HumidityRange
{
    public double Min { get; set; }
    public double Max { get; set; }
}

/// <summary>
/// pH range
/// </summary>
public class pHRange
{
    public double Min { get; set; }
    public double Max { get; set; }
} 