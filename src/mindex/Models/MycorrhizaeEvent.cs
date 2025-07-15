using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace NatureOS.MINDEX.Models;

/// <summary>
/// Core event model following the Mycorrhizae Protocol specification
/// Represents an immutable biological interaction event
/// </summary>
public class MycorrhizaeEvent
{
    /// <summary>
    /// ULID for chronological ordering and global uniqueness
    /// </summary>
    [JsonPropertyName("event_id")]
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// ISO 8601 timestamp with microsecond precision
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// EUI-64 identifier or certificate thumbprint of source device
    /// </summary>
    [JsonPropertyName("source_device")]
    public string SourceDevice { get; set; } = string.Empty;

    /// <summary>
    /// Domain classification (FUNGA.signaling, FLORA.photosynthesis, etc.)
    /// </summary>
    [JsonPropertyName("kingdom_domain")]
    public string KingdomDomain { get; set; } = string.Empty;

    /// <summary>
    /// Raw sensor payload or measurement data
    /// </summary>
    [JsonPropertyName("signal_vector")]
    public object? SignalVector { get; set; }

    /// <summary>
    /// JSON-LD semantic annotation of the signal
    /// </summary>
    [JsonPropertyName("decoded_meaning")]
    public DecodedMeaning? DecodedMeaning { get; set; }

    /// <summary>
    /// External references (DOI, specimen ID, GPS coordinates, etc.)
    /// </summary>
    [JsonPropertyName("references")]
    public EventReferences? References { get; set; }

    /// <summary>
    /// Event processing metadata
    /// </summary>
    [JsonPropertyName("metadata")]
    public EventMetadata? Metadata { get; set; }

    /// <summary>
    /// Time-to-live for the event (in seconds, -1 for no expiry)
    /// </summary>
    [JsonPropertyName("ttl")]
    public int? TTL { get; set; }
}

/// <summary>
/// JSON-LD semantic annotation structure
/// </summary>
public class DecodedMeaning
{
    /// <summary>
    /// JSON-LD context
    /// </summary>
    [JsonPropertyName("@context")]
    public string? Context { get; set; }

    /// <summary>
    /// Resource type
    /// </summary>
    [JsonPropertyName("@type")]
    public string? Type { get; set; }

    /// <summary>
    /// Ontological classification
    /// </summary>
    [JsonPropertyName("ontology")]
    public Dictionary<string, object>? Ontology { get; set; }

    /// <summary>
    /// Confidence score (0.0 - 1.0)
    /// </summary>
    [JsonPropertyName("confidence")]
    public double? Confidence { get; set; }

    /// <summary>
    /// Processing algorithm used
    /// </summary>
    [JsonPropertyName("algorithm")]
    public string? Algorithm { get; set; }

    /// <summary>
    /// Additional semantic annotations
    /// </summary>
    [JsonPropertyName("annotations")]
    public Dictionary<string, object>? Annotations { get; set; }
}

/// <summary>
/// External references and identifiers
/// </summary>
public class EventReferences
{
    /// <summary>
    /// Digital Object Identifier
    /// </summary>
    [JsonPropertyName("doi")]
    public string? DOI { get; set; }

    /// <summary>
    /// Specimen or sample identifier
    /// </summary>
    [JsonPropertyName("specimen_id")]
    public string? SpecimenId { get; set; }

    /// <summary>
    /// GPS coordinates
    /// </summary>
    [JsonPropertyName("location")]
    public GeoLocation? Location { get; set; }

    /// <summary>
    /// Taxonomic classification
    /// </summary>
    [JsonPropertyName("taxonomy")]
    public TaxonomicClassification? Taxonomy { get; set; }

    /// <summary>
    /// Related experiment or study identifier
    /// </summary>
    [JsonPropertyName("study_id")]
    public string? StudyId { get; set; }

    /// <summary>
    /// Environmental context identifiers
    /// </summary>
    [JsonPropertyName("environment")]
    public EnvironmentalContext? Environment { get; set; }
}

/// <summary>
/// GPS coordinates and spatial information
/// </summary>
public class GeoLocation
{
    /// <summary>
    /// Latitude in decimal degrees
    /// </summary>
    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    /// <summary>
    /// Longitude in decimal degrees
    /// </summary>
    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    /// <summary>
    /// Elevation in meters above sea level
    /// </summary>
    [JsonPropertyName("elevation")]
    public double? Elevation { get; set; }

    /// <summary>
    /// Spatial accuracy in meters
    /// </summary>
    [JsonPropertyName("accuracy")]
    public double? Accuracy { get; set; }
}

/// <summary>
/// Taxonomic classification information
/// </summary>
public class TaxonomicClassification
{
    /// <summary>
    /// Kingdom (Fungi, Plantae, Animalia)
    /// </summary>
    [JsonPropertyName("kingdom")]
    public string? Kingdom { get; set; }

    /// <summary>
    /// Phylum
    /// </summary>
    [JsonPropertyName("phylum")]
    public string? Phylum { get; set; }

    /// <summary>
    /// Class
    /// </summary>
    [JsonPropertyName("class")]
    public string? Class { get; set; }

    /// <summary>
    /// Order
    /// </summary>
    [JsonPropertyName("order")]
    public string? Order { get; set; }

    /// <summary>
    /// Family
    /// </summary>
    [JsonPropertyName("family")]
    public string? Family { get; set; }

    /// <summary>
    /// Genus
    /// </summary>
    [JsonPropertyName("genus")]
    public string? Genus { get; set; }

    /// <summary>
    /// Species
    /// </summary>
    [JsonPropertyName("species")]
    public string? Species { get; set; }

    /// <summary>
    /// Scientific name
    /// </summary>
    [JsonPropertyName("scientific_name")]
    public string? ScientificName { get; set; }
}

/// <summary>
/// Environmental context information
/// </summary>
public class EnvironmentalContext
{
    /// <summary>
    /// Habitat type
    /// </summary>
    [JsonPropertyName("habitat")]
    public string? Habitat { get; set; }

    /// <summary>
    /// Substrate information
    /// </summary>
    [JsonPropertyName("substrate")]
    public string? Substrate { get; set; }

    /// <summary>
    /// Temperature at time of observation (Celsius)
    /// </summary>
    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    /// <summary>
    /// Humidity percentage
    /// </summary>
    [JsonPropertyName("humidity")]
    public double? Humidity { get; set; }

    /// <summary>
    /// pH level
    /// </summary>
    [JsonPropertyName("ph")]
    public double? pH { get; set; }

    /// <summary>
    /// Additional environmental parameters
    /// </summary>
    [JsonPropertyName("parameters")]
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// Event processing and system metadata
/// </summary>
public class EventMetadata
{
    /// <summary>
    /// Processing pipeline version
    /// </summary>
    [JsonPropertyName("pipeline_version")]
    public string? PipelineVersion { get; set; }

    /// <summary>
    /// Ingestion timestamp
    /// </summary>
    [JsonPropertyName("ingested_at")]
    public DateTime? IngestedAt { get; set; }

    /// <summary>
    /// Processing timestamp
    /// </summary>
    [JsonPropertyName("processed_at")]
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Tenant or organization identifier
    /// </summary>
    [JsonPropertyName("tenant_id")]
    public string? TenantId { get; set; }

    /// <summary>
    /// Data quality score
    /// </summary>
    [JsonPropertyName("quality_score")]
    public double? QualityScore { get; set; }

    /// <summary>
    /// Processing flags and status
    /// </summary>
    [JsonPropertyName("flags")]
    public List<string>? Flags { get; set; }

    /// <summary>
    /// Checksum for data integrity
    /// </summary>
    [JsonPropertyName("checksum")]
    public string? Checksum { get; set; }
} 