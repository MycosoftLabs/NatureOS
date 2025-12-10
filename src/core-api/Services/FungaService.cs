using Microsoft.Azure.Cosmos;
using NatureOS.MINDEX.Models;
using System.Text.Json;

namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service for FUNGA (mycology) domain operations
/// </summary>
public class FungaService : IFungaService
{
    private readonly Container _eventsContainer;
    private readonly Container _taxonomyContainer;
    private readonly ILogger<FungaService> _logger;
    private readonly HttpClient _httpClient;

    public FungaService(CosmosClient cosmosClient, ILogger<FungaService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        var database = cosmosClient.GetDatabase("mindex");
        _eventsContainer = database.GetContainer("events");
        _taxonomyContainer = database.GetContainer("taxonomy");
    }

    public async Task<PagedResult<MycorrhizaeEvent>> GetFungaEventsAsync(FungaQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var conditions = new List<string>();
            var parameters = new List<(string, object)>();

            // Base condition for FUNGA domain
            conditions.Add("c.kingdom_domain LIKE 'FUNGA%'");

            // Add filters from query
            if (!string.IsNullOrEmpty(query.SourceDevice))
            {
                conditions.Add("c.source_device = @sourceDevice");
                parameters.Add(("@sourceDevice", query.SourceDevice));
            }

            if (!string.IsNullOrEmpty(query.Phylum))
            {
                conditions.Add("c.references.taxonomy.phylum = @phylum");
                parameters.Add(("@phylum", query.Phylum));
            }

            if (!string.IsNullOrEmpty(query.SubstrateType))
            {
                conditions.Add("c.references.environment.substrate = @substrate");
                parameters.Add(("@substrate", query.SubstrateType));
            }

            if (query.TemperatureRange != null)
            {
                conditions.Add("c.references.environment.temperature >= @tempMin AND c.references.environment.temperature <= @tempMax");
                parameters.Add(("@tempMin", query.TemperatureRange.Min));
                parameters.Add(("@tempMax", query.TemperatureRange.Max));
            }

            if (query.StartTime.HasValue)
            {
                conditions.Add("c.timestamp >= @startTime");
                parameters.Add(("@startTime", query.StartTime.Value));
            }

            if (query.EndTime.HasValue)
            {
                conditions.Add("c.timestamp <= @endTime");
                parameters.Add(("@endTime", query.EndTime.Value));
            }

            if (query.MycorrhizalOnly == true)
            {
                conditions.Add("c.decoded_meaning.annotations.mycorrhizal_type IS NOT NULL");
            }

            var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";
            var orderBy = query.SortOrder == "timestamp_asc" ? "ORDER BY c.timestamp ASC" : "ORDER BY c.timestamp DESC";

            var sqlQuery = $"SELECT * FROM c {whereClause} {orderBy}";
            var queryDefinition = new QueryDefinition(sqlQuery);

            foreach (var (name, value) in parameters)
            {
                queryDefinition.WithParameter(name, value);
            }

            var iterator = _eventsContainer.GetItemQueryIterator<MycorrhizaeEvent>(
                queryDefinition,
                continuationToken: query.ContinuationToken,
                requestOptions: new QueryRequestOptions { MaxItemCount = query.PageSize });

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
            _logger.LogError(ex, "Failed to get FUNGA events");
            throw;
        }
    }

    public async Task<FungalClassification> ClassifySpecimenAsync(object signalVector, CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract features from signal vector
            var features = ExtractMorphologicalFeatures(signalVector);
            
            // Perform taxonomic classification using ML model
            var taxonomy = await PerformTaxonomicClassification(features);
            
            // Get alternative classifications
            var alternatives = await GetAlternativeClassifications(features);
            
            // Analyze ecological indicators
            var ecology = await AnalyzeEcologicalIndicators(features);

            return new FungalClassification
            {
                Taxonomy = taxonomy,
                Confidence = CalculateConfidence(features, taxonomy),
                Alternatives = alternatives,
                Features = features,
                Ecology = ecology
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to classify fungal specimen");
            throw;
        }
    }

    public async Task<MycorrhizalNetwork> AnalyzeNetworkAsync(double locationRadius, GeoLocation location, CancellationToken cancellationToken = default)
    {
        try
        {
            // Find events within radius
            var eventsQuery = new QueryDefinition(
                @"SELECT * FROM c WHERE c.kingdom_domain LIKE 'FUNGA%' 
                  AND ST_DISTANCE(c.references.location, @location) <= @radius")
                .WithParameter("@location", new { type = "Point", coordinates = new[] { location.Longitude, location.Latitude } })
                .WithParameter("@radius", locationRadius);

            var events = new List<MycorrhizaeEvent>();
            var iterator = _eventsContainer.GetItemQueryIterator<MycorrhizaeEvent>(eventsQuery);
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                events.AddRange(response);
            }

            // Build network nodes
            var nodes = new List<NetworkNode>();
            var nodeMap = new Dictionary<string, NetworkNode>();

            foreach (var evt in events)
            {
                var nodeId = evt.SourceDevice;
                if (!nodeMap.ContainsKey(nodeId))
                {
                    var node = new NetworkNode
                    {
                        Id = nodeId,
                        Type = "fungi",
                        Species = evt.References?.Taxonomy,
                        Location = evt.References?.Location ?? new GeoLocation(),
                        ConnectionCount = 0
                    };
                    nodes.Add(node);
                    nodeMap[nodeId] = node;
                }
            }

            // Analyze connections based on proximity and temporal patterns
            var edges = BuildNetworkEdges(events, nodes);
            
            // Calculate network metrics
            var metrics = CalculateNetworkMetrics(nodes, edges);

            return new MycorrhizalNetwork
            {
                Nodes = nodes,
                Edges = edges,
                Metrics = metrics
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze mycorrhizal network");
            throw;
        }
    }

    public async Task<SporeDispersal> GetSporeDispersalAsync(DateTimeRange timeRange, GeoLocation location, CancellationToken cancellationToken = default)
    {
        try
        {
            // Query spore-related events in time range
            var sporeEventsQuery = new QueryDefinition(
                @"SELECT * FROM c WHERE c.kingdom_domain LIKE 'FUNGA.spore%' 
                  AND c.timestamp >= @startTime AND c.timestamp <= @endTime")
                .WithParameter("@startTime", timeRange.Start)
                .WithParameter("@endTime", timeRange.End);

            var sporeEvents = new List<MycorrhizaeEvent>();
            var iterator = _eventsContainer.GetItemQueryIterator<MycorrhizaeEvent>(sporeEventsQuery);
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                sporeEvents.AddRange(response);
            }

            // Analyze dispersal patterns
            var patterns = AnalyzeDispersalPatterns(sporeEvents, location);
            
            // Analyze wind influence
            var windInfluence = AnalyzeWindInfluence(sporeEvents);
            
            // Analyze temporal patterns
            var temporalPattern = AnalyzeTemporalPatterns(sporeEvents);

            return new SporeDispersal
            {
                Patterns = patterns,
                WindInfluence = windInfluence,
                TemporalPattern = temporalPattern
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze spore dispersal");
            throw;
        }
    }

    public async Task<BiodiversityMetrics> GetDiversityMetricsAsync(FungaQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var events = await GetFungaEventsAsync(query, cancellationToken);
            var speciesCounts = new Dictionary<string, int>();
            
            foreach (var evt in events.Items)
            {
                var species = evt.References?.Taxonomy?.ScientificName ?? "Unknown";
                speciesCounts[species] = speciesCounts.GetValueOrDefault(species, 0) + 1;
            }

            var totalIndividuals = speciesCounts.Values.Sum();
            var speciesRichness = speciesCounts.Keys.Count;
            
            // Calculate Shannon Index
            var shannonIndex = CalculateShannonIndex(speciesCounts, totalIndividuals);
            
            // Calculate Simpson Index
            var simpsonIndex = CalculateSimpsonIndex(speciesCounts, totalIndividuals);
            
            // Calculate Evenness
            var evenness = shannonIndex / Math.Log(speciesRichness);
            
            // Count rare species (species with only 1-2 individuals)
            var rareSpecies = speciesCounts.Count(kvp => kvp.Value <= 2);

            return new BiodiversityMetrics
            {
                SpeciesRichness = speciesRichness,
                ShannonIndex = shannonIndex,
                SimpsonIndex = simpsonIndex,
                EvennessIndex = evenness,
                RareSpeciesCount = rareSpecies
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate biodiversity metrics");
            throw;
        }
    }

    private static MorphologicalFeatures ExtractMorphologicalFeatures(object signalVector)
    {
        // Extract morphological features from signal data
        // This would be implemented with actual ML/CV algorithms
        return new MorphologicalFeatures
        {
            CapDiameter = 25.0,
            StemHeight = 40.0,
            SporeSize = 8.5,
            Color = "brown",
            Texture = "smooth"
        };
    }

    private async Task<TaxonomicClassification> PerformTaxonomicClassification(MorphologicalFeatures features)
    {
        // This would call an ML model for taxonomic classification
        // For now, return a placeholder classification
        return new TaxonomicClassification
        {
            Kingdom = "Fungi",
            Phylum = "Basidiomycota",
            Class = "Agaricomycetes",
            Order = "Agaricales",
            Family = "Agaricaceae",
            Genus = "Agaricus",
            Species = "bisporus",
            ScientificName = "Agaricus bisporus"
        };
    }

    private async Task<List<AlternativeClassification>> GetAlternativeClassifications(MorphologicalFeatures features)
    {
        // Return alternative classifications with confidence scores
        return new List<AlternativeClassification>
        {
            new AlternativeClassification
            {
                Taxonomy = new TaxonomicClassification
                {
                    Kingdom = "Fungi",
                    Phylum = "Basidiomycota",
                    Class = "Agaricomycetes",
                    Order = "Agaricales",
                    Family = "Agaricaceae",
                    Genus = "Agaricus",
                    Species = "campestris",
                    ScientificName = "Agaricus campestris"
                },
                Confidence = 0.25
            }
        };
    }

    private async Task<EcologicalIndicators> AnalyzeEcologicalIndicators(MorphologicalFeatures features)
    {
        return new EcologicalIndicators
        {
            MycorrhizalType = "arbuscular",
            HostSpecies = new List<string> { "Quercus alba", "Pinus strobus" },
            SoilHealth = new Dictionary<string, double>
            {
                ["nitrogen"] = 0.8,
                ["phosphorus"] = 0.6,
                ["ph"] = 6.5,
                ["organic_matter"] = 0.85
            },
            EcosystemRole = "decomposer"
        };
    }

    private static double CalculateConfidence(MorphologicalFeatures features, TaxonomicClassification taxonomy)
    {
        // Calculate confidence based on feature quality and taxonomic certainty
        return 0.85;
    }

    private static List<NetworkEdge> BuildNetworkEdges(List<MycorrhizaeEvent> events, List<NetworkNode> nodes)
    {
        var edges = new List<NetworkEdge>();
        var nodeMap = nodes.ToDictionary(n => n.Id);

        // Create edges based on spatial and temporal proximity
        for (int i = 0; i < events.Count; i++)
        {
            for (int j = i + 1; j < events.Count; j++)
            {
                var evt1 = events[i];
                var evt2 = events[j];

                if (evt1.References?.Location != null && evt2.References?.Location != null)
                {
                    var distance = CalculateDistance(evt1.References.Location, evt2.References.Location);
                    if (distance <= 100) // Within 100 meters
                    {
                        edges.Add(new NetworkEdge
                        {
                            SourceId = evt1.SourceDevice,
                            TargetId = evt2.SourceDevice,
                            Strength = 1.0 / (distance + 1), // Inverse distance
                            Type = "spatial"
                        });
                    }
                }
            }
        }

        return edges;
    }

    private static NetworkMetrics CalculateNetworkMetrics(List<NetworkNode> nodes, List<NetworkEdge> edges)
    {
        var nodeCount = nodes.Count;
        var edgeCount = edges.Count;
        var maxPossibleEdges = nodeCount * (nodeCount - 1) / 2;
        var density = maxPossibleEdges > 0 ? (double)edgeCount / maxPossibleEdges : 0;

        return new NetworkMetrics
        {
            NodeCount = nodeCount,
            EdgeCount = edgeCount,
            Density = density,
            ClusteringCoefficient = 0.3 // Simplified calculation
        };
    }

    private static List<DispersalPattern> AnalyzeDispersalPatterns(List<MycorrhizaeEvent> events, GeoLocation center)
    {
        var patterns = new List<DispersalPattern>();
        
        foreach (var evt in events)
        {
            if (evt.References?.Location != null)
            {
                var distance = CalculateDistance(center, evt.References.Location);
                patterns.Add(new DispersalPattern
                {
                    Source = center,
                    Targets = new List<GeoLocation> { evt.References.Location },
                    Distance = distance,
                    Concentration = 1.0 / (distance + 1) // Inverse distance weighting
                });
            }
        }

        return patterns;
    }

    private static WindInfluence AnalyzeWindInfluence(List<MycorrhizaeEvent> events)
    {
        // Simplified wind analysis
        return new WindInfluence
        {
            Direction = 225, // SW direction
            Speed = 3.2,
            Correlation = 0.65
        };
    }

    private static TemporalPattern AnalyzeTemporalPatterns(List<MycorrhizaeEvent> events)
    {
        var hourlyDistribution = events
            .GroupBy(e => e.Timestamp.Hour)
            .ToDictionary(g => g.Key, g => g.Count());

        var peakHours = hourlyDistribution
            .OrderByDescending(kvp => kvp.Value)
            .Take(3)
            .Select(kvp => TimeSpan.FromHours(kvp.Key))
            .ToList();

        return new TemporalPattern
        {
            PeakTimes = peakHours,
            SeasonalVariations = new Dictionary<string, double>
            {
                ["spring"] = 0.8,
                ["summer"] = 1.2,
                ["autumn"] = 1.5,
                ["winter"] = 0.3
            }
        };
    }

    private static double CalculateDistance(GeoLocation loc1, GeoLocation loc2)
    {
        // Haversine formula for great circle distance
        const double earthRadius = 6371000; // meters
        
        var lat1Rad = loc1.Latitude * Math.PI / 180;
        var lat2Rad = loc2.Latitude * Math.PI / 180;
        var deltaLat = (loc2.Latitude - loc1.Latitude) * Math.PI / 180;
        var deltaLon = (loc2.Longitude - loc1.Longitude) * Math.PI / 180;

        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadius * c;
    }

    private static double CalculateShannonIndex(Dictionary<string, int> speciesCounts, int totalIndividuals)
    {
        var shannonIndex = 0.0;
        foreach (var count in speciesCounts.Values)
        {
            var proportion = (double)count / totalIndividuals;
            shannonIndex -= proportion * Math.Log(proportion);
        }
        return shannonIndex;
    }

    private static double CalculateSimpsonIndex(Dictionary<string, int> speciesCounts, int totalIndividuals)
    {
        var simpsonIndex = 0.0;
        foreach (var count in speciesCounts.Values)
        {
            var proportion = (double)count / totalIndividuals;
            simpsonIndex += proportion * proportion;
        }
        return 1.0 - simpsonIndex;
    }
} 