using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NatureOS.MINDEX.Models;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service for integrating with external scientific databases and APIs
/// </summary>
public class ExternalDataIntegrationService : IExternalDataIntegrationService
{
    private readonly HttpClient _httpClient;
    private readonly CosmosClient _cosmosClient;
    private readonly ILogger<ExternalDataIntegrationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly Container _compoundsContainer;
    private readonly Container _taxonomyContainer;
    private readonly Container _eventsContainer;

    public ExternalDataIntegrationService(
        HttpClient httpClient,
        CosmosClient cosmosClient,
        ILogger<ExternalDataIntegrationService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _cosmosClient = cosmosClient;
        _logger = logger;
        _configuration = configuration;
        
        var database = _cosmosClient.GetDatabase("mindex");
        _compoundsContainer = database.GetContainer("compounds");
        _taxonomyContainer = database.GetContainer("taxonomy");
        _eventsContainer = database.GetContainer("events");
    }

    /// <summary>
    /// Scrape and inject data from FungiDB
    /// </summary>
    public async Task<DataInjectionResult> InjectFungiDbDataAsync(string? genusFilter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting FungiDB data injection for genus: {Genus}", genusFilter ?? "all");
            
            var apiKey = _configuration["ExternalApis:FungiDB:ApiKey"];
            var baseUrl = "https://api.fungidb.org/v1/";
            
            var injectedCount = 0;
            var errorCount = 0;
            
            // Build query URL
            var queryUrl = $"{baseUrl}species";
            if (!string.IsNullOrEmpty(genusFilter))
            {
                queryUrl += $"?genus={Uri.EscapeDataString(genusFilter)}";
            }
            
            // Add API key header
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
            
            var response = await _httpClient.GetStringAsync(queryUrl);
            var fungiData = JsonSerializer.Deserialize<FungiDbResponse>(response);
            
            if (fungiData?.Species != null)
            {
                foreach (var species in fungiData.Species)
                {
                    try
                    {
                        var taxonomyRecord = new TaxonomyRecord
                        {
                            Id = $"fungidb_{species.Id}",
                            Kingdom = "Fungi",
                            Phylum = species.Phylum,
                            Class = species.Class,
                            Order = species.Order,
                            Family = species.Family,
                            Genus = species.Genus,
                            Species = species.SpeciesName,
                            Authority = species.Authority,
                            CommonNames = species.CommonNames ?? new List<string>(),
                            Synonyms = species.Synonyms ?? new List<string>(),
                            Description = species.Description,
                            Habitat = species.Habitat,
                            Distribution = species.Distribution,
                            Source = "FungiDB",
                            LastUpdated = DateTime.UtcNow,
                            Metadata = new Dictionary<string, object>
                            {
                                ["fungidb_id"] = species.Id,
                                ["morphology"] = species.Morphology ?? new {},
                                ["ecology"] = species.Ecology ?? new {},
                                ["images"] = species.Images ?? new List<string>()
                            }
                        };
                        
                        await _taxonomyContainer.UpsertItemAsync(taxonomyRecord, new PartitionKey(taxonomyRecord.Genus));
                        injectedCount++;
                        
                        _logger.LogDebug("Injected taxonomy record for {Species}", species.SpeciesName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to inject FungiDB species: {Species}", species.SpeciesName);
                        errorCount++;
                    }
                }
            }
            
            return new DataInjectionResult
            {
                Source = "FungiDB",
                Success = true,
                RecordsProcessed = fungiData?.Species?.Count ?? 0,
                RecordsInjected = injectedCount,
                ErrorCount = errorCount,
                CompletedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inject FungiDB data");
            return new DataInjectionResult
            {
                Source = "FungiDB",
                Success = false,
                ErrorMessage = ex.Message,
                CompletedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Scrape and inject data from iNaturalist
    /// </summary>
    public async Task<DataInjectionResult> InjectINaturalistDataAsync(string? locationFilter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting iNaturalist data injection for location: {Location}", locationFilter ?? "global");
            
            var injectedCount = 0;
            var errorCount = 0;
            
            // iNaturalist API endpoint for fungal observations
            var baseUrl = "https://api.inaturalist.org/v1/observations";
            var queryParams = new List<string>
            {
                "iconic_taxa=Fungi",
                "quality_grade=research",
                "per_page=200",
                "order=desc",
                "order_by=created_at"
            };
            
            if (!string.IsNullOrEmpty(locationFilter))
            {
                queryParams.Add($"place_id={Uri.EscapeDataString(locationFilter)}");
            }
            
            var queryUrl = $"{baseUrl}?{string.Join("&", queryParams)}";
            
            var response = await _httpClient.GetStringAsync(queryUrl);
            var inatData = JsonSerializer.Deserialize<INaturalistResponse>(response);
            
            if (inatData?.Results != null)
            {
                foreach (var observation in inatData.Results)
                {
                    try
                    {
                        if (observation.Taxon?.Name != null)
                        {
                            // Create enriched event from iNaturalist observation
                            var enrichedEvent = new MycorrhizaeEvent
                            {
                                EventId = $"inat_{observation.Id}",
                                Timestamp = observation.CreatedAt,
                                SourceDevice = "iNaturalist",
                                KingdomDomain = "FUNGA.observation",
                                SignalVector = new
                                {
                                    observation_id = observation.Id,
                                    photos = observation.Photos?.Select(p => p.Url).ToList() ?? new List<string>(),
                                    sounds = observation.Sounds?.Select(s => s.Url).ToList() ?? new List<string>()
                                },
                                References = new EventReferences
                                {
                                    Location = observation.Location != null ? new GeoLocation
                                    {
                                        Latitude = observation.Location.Latitude,
                                        Longitude = observation.Location.Longitude,
                                        Accuracy = observation.PositionalAccuracy
                                    } : null,
                                    // Taxonomy = new TaxonomyRecord // Type mismatch - disabled for compilation
                                    // {
                                    //     Kingdom = observation.Taxon.Kingdom?.Name,
                                    //     Phylum = observation.Taxon.Phylum?.Name,
                                    //     Class = observation.Taxon.Class?.Name,
                                    //     Order = observation.Taxon.Order?.Name,
                                    //     Family = observation.Taxon.Family?.Name,
                                    //     Genus = observation.Taxon.Genus?.Name,
                                    //     Species = observation.Taxon.Name
                                    // },
                                    Environment = new EnvironmentalContext
                                    {
                                        Habitat = observation.PlaceGuess,
                                        Parameters = new Dictionary<string, object>
                                        {
                                            ["observed_on"] = observation.ObservedOn,
                                            ["quality_grade"] = observation.QualityGrade,
                                            ["research_grade"] = observation.QualityGrade == "research",
                                            ["community_taxon"] = observation.CommunityTaxon?.Name
                                        }
                                    }
                                },
                                Metadata = new EventMetadata
                                {
                                    IngestedAt = DateTime.UtcNow,
                                    PipelineVersion = "2.0.0",
                                    // Source = "iNaturalist", // Property doesn't exist in EventMetadata
                                    TenantId = "external_data",
                                    QualityScore = observation.QualityGrade == "research" ? 1.0 : 0.7
                                }
                            };
                            
                            await _eventsContainer.UpsertItemAsync(enrichedEvent, new PartitionKey(enrichedEvent.SourceDevice));
                            injectedCount++;
                            
                            _logger.LogDebug("Injected iNaturalist observation: {Species}", observation.Taxon.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to inject iNaturalist observation: {Id}", observation.Id);
                        errorCount++;
                    }
                }
            }
            
            return new DataInjectionResult
            {
                Source = "iNaturalist",
                Success = true,
                RecordsProcessed = inatData?.Results?.Count ?? 0,
                RecordsInjected = injectedCount,
                ErrorCount = errorCount,
                CompletedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inject iNaturalist data");
            return new DataInjectionResult
            {
                Source = "iNaturalist",
                Success = false,
                ErrorMessage = ex.Message,
                CompletedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Scrape and inject data from MycoBank
    /// </summary>
    public async Task<DataInjectionResult> InjectMycoBankDataAsync(string? nameFilter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting MycoBank data injection for name: {Name}", nameFilter ?? "all");
            
            var injectedCount = 0;
            var errorCount = 0;
            
            // MycoBank API for taxonomic data
            var baseUrl = "https://www.mycobank.org/services/generic/ws.asmx/MBNameOtherDestinations";
            
            var queryParams = new Dictionary<string, string>
            {
                ["format"] = "json",
                ["epithet"] = nameFilter ?? "",
                ["includeBasionymSynonyms"] = "true",
                ["includeAvailable"] = "true"
            };
            
            var content = new FormUrlEncodedContent(queryParams);
            var response = await _httpClient.PostAsync(baseUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            var mycoBankData = JsonSerializer.Deserialize<MycoBankResponse>(responseContent);
            
            if (mycoBankData?.Results != null)
            {
                foreach (var record in mycoBankData.Results)
                {
                    try
                    {
                        var taxonomyRecord = new TaxonomyRecord
                        {
                            Id = $"mycobank_{record.MBNumber}",
                            Kingdom = "Fungi",
                            Phylum = record.HigherClassification?.Phylum,
                            Class = record.HigherClassification?.Class,
                            Order = record.HigherClassification?.Order,
                            Family = record.HigherClassification?.Family,
                            Genus = record.Genus,
                            Species = record.EpithetLowerCase,
                            Authority = record.Authors,
                            Publication = record.Publication,
                            PublicationYear = record.YearOfPublication,
                            Source = "MycoBank",
                            LastUpdated = DateTime.UtcNow,
                            Metadata = new Dictionary<string, object>
                            {
                                ["mycobank_number"] = record.MBNumber,
                                ["status"] = record.Status ?? "",
                                ["basionym"] = record.Basionym ?? "",
                                ["synonyms"] = record.Synonyms ?? new List<string>(),
                                ["type_specimen"] = record.TypeSpecimen ?? "",
                                ["literature_references"] = record.References ?? new List<string>()
                            }
                        };
                        
                        await _taxonomyContainer.UpsertItemAsync(taxonomyRecord, new PartitionKey(taxonomyRecord.Genus ?? "unknown"));
                        injectedCount++;
                        
                        _logger.LogDebug("Injected MycoBank record: {Species}", record.EpithetLowerCase);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to inject MycoBank record: {MBNumber}", record.MBNumber);
                        errorCount++;
                    }
                }
            }
            
            return new DataInjectionResult
            {
                Source = "MycoBank",
                Success = true,
                RecordsProcessed = mycoBankData?.Results?.Count ?? 0,
                RecordsInjected = injectedCount,
                ErrorCount = errorCount,
                CompletedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inject MycoBank data");
            return new DataInjectionResult
            {
                Source = "MycoBank",
                Success = false,
                ErrorMessage = ex.Message,
                CompletedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Scrape and inject chemical compound data from ChemPub and Science Direct
    /// </summary>
    public async Task<DataInjectionResult> InjectChemicalCompoundDataAsync(string? compoundFilter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting chemical compound data injection for compound: {Compound}", compoundFilter ?? "fungal metabolites");
            
            var injectedCount = 0;
            var errorCount = 0;
            
            // Multi-source compound data injection
            var tasks = new List<Task<CompoundDataResult>>
            {
                ScrapeChemPubDataAsync(compoundFilter),
                ScrapeScienceDirectDataAsync(compoundFilter),
                ScrapePubChemDataAsync(compoundFilter)
            };
            
            var results = await Task.WhenAll(tasks);
            
            foreach (var result in results)
            {
                if (result.Success && result.Compounds != null)
                {
                    foreach (var compound in result.Compounds)
                    {
                        try
                        {
                            var compoundRecord = new CompoundRecord
                            {
                                Id = $"{result.Source.ToLower()}_{compound.Id}",
                                Name = compound.Name,
                                Formula = compound.MolecularFormula,
                                MolecularWeight = compound.MolecularWeight,
                                SMILES = compound.SMILES,
                                InChI = compound.InChI,
                                CASNumber = compound.CASNumber,
                                Source = result.Source,
                                BiologicalActivity = compound.BiologicalActivity,
                                FungalSources = compound.FungalSources ?? new List<string>(),
                                Properties = compound.Properties ?? new Dictionary<string, object>(),
                                References = compound.References ?? new List<string>(),
                                LastUpdated = DateTime.UtcNow,
                                Metadata = new Dictionary<string, object>
                                {
                                    ["extraction_date"] = DateTime.UtcNow,
                                    ["data_quality"] = compound.DataQuality,
                                    ["original_id"] = compound.Id
                                }
                            };
                            
                            await _compoundsContainer.UpsertItemAsync(compoundRecord, new PartitionKey(compoundRecord.Name));
                            injectedCount++;
                            
                            _logger.LogDebug("Injected compound record: {Compound}", compound.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to inject compound: {Compound}", compound.Name);
                            errorCount++;
                        }
                    }
                }
            }
            
            return new DataInjectionResult
            {
                Source = "Chemical Databases",
                Success = true,
                RecordsProcessed = results.Sum(r => r.Compounds?.Count ?? 0),
                RecordsInjected = injectedCount,
                ErrorCount = errorCount,
                CompletedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inject chemical compound data");
            return new DataInjectionResult
            {
                Source = "Chemical Databases",
                Success = false,
                ErrorMessage = ex.Message,
                CompletedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Run comprehensive data enrichment from all external sources
    /// </summary>
    public async Task<DataEnrichmentResult> RunComprehensiveDataEnrichmentAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting comprehensive data enrichment from all external sources");
        
        var enrichmentTasks = new List<Task<DataInjectionResult>>
        {
            InjectFungiDbDataAsync(),
            InjectINaturalistDataAsync(),
            InjectMycoBankDataAsync(),
            InjectChemicalCompoundDataAsync()
        };
        
        var results = await Task.WhenAll(enrichmentTasks);
        
        return new DataEnrichmentResult
        {
            Success = results.All(r => r.Success),
            Sources = results.Select(r => r.Source).ToList(),
            TotalRecordsProcessed = results.Sum(r => r.RecordsProcessed),
            TotalRecordsInjected = results.Sum(r => r.RecordsInjected),
            TotalErrors = results.Sum(r => r.ErrorCount),
            IndividualResults = results.ToList(),
            CompletedAt = DateTime.UtcNow
        };
    }

    // Private helper methods
    private async Task<CompoundDataResult> ScrapeChemPubDataAsync(string? filter)
    {
        try
        {
            // Implement ChemPub scraping logic
            // This would use their API or web scraping
            return new CompoundDataResult
            {
                Source = "ChemPub",
                Success = true,
                Compounds = new List<CompoundData>() // Implementation needed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape ChemPub data");
            return new CompoundDataResult { Source = "ChemPub", Success = false };
        }
    }

    private async Task<CompoundDataResult> ScrapeScienceDirectDataAsync(string? filter)
    {
        try
        {
            // Implement Science Direct API integration
            var apiKey = _configuration["ExternalApis:ScienceDirect:ApiKey"];
            
            // Implementation needed for Science Direct API
            return new CompoundDataResult
            {
                Source = "ScienceDirect",
                Success = true,
                Compounds = new List<CompoundData>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape Science Direct data");
            return new CompoundDataResult { Source = "ScienceDirect", Success = false };
        }
    }

    private async Task<CompoundDataResult> ScrapePubChemDataAsync(string? filter)
    {
        try
        {
            // Implement PubChem API integration
            var baseUrl = "https://pubchem.ncbi.nlm.nih.gov/rest/pug/compound/name/";
            
            // Implementation needed for PubChem API
            return new CompoundDataResult
            {
                Source = "PubChem",
                Success = true,
                Compounds = new List<CompoundData>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape PubChem data");
            return new CompoundDataResult { Source = "PubChem", Success = false };
        }
    }
}

// Interface definition
public interface IExternalDataIntegrationService
{
    Task<DataInjectionResult> InjectFungiDbDataAsync(string? genusFilter = null, CancellationToken cancellationToken = default);
    Task<DataInjectionResult> InjectINaturalistDataAsync(string? locationFilter = null, CancellationToken cancellationToken = default);
    Task<DataInjectionResult> InjectMycoBankDataAsync(string? nameFilter = null, CancellationToken cancellationToken = default);
    Task<DataInjectionResult> InjectChemicalCompoundDataAsync(string? compoundFilter = null, CancellationToken cancellationToken = default);
    Task<DataEnrichmentResult> RunComprehensiveDataEnrichmentAsync(CancellationToken cancellationToken = default);
}

// Supporting data models
public class DataInjectionResult
{
    public string Source { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int RecordsProcessed { get; set; }
    public int RecordsInjected { get; set; }
    public int ErrorCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CompletedAt { get; set; }
}

public class DataEnrichmentResult
{
    public bool Success { get; set; }
    public List<string> Sources { get; set; } = new();
    public int TotalRecordsProcessed { get; set; }
    public int TotalRecordsInjected { get; set; }
    public int TotalErrors { get; set; }
    public List<DataInjectionResult> IndividualResults { get; set; } = new();
    public DateTime CompletedAt { get; set; }
}

public class CompoundDataResult
{
    public string Source { get; set; } = string.Empty;
    public bool Success { get; set; }
    public List<CompoundData>? Compounds { get; set; }
}

public class CompoundData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? MolecularFormula { get; set; }
    public double? MolecularWeight { get; set; }
    public string? SMILES { get; set; }
    public string? InChI { get; set; }
    public string? CASNumber { get; set; }
    public string? BiologicalActivity { get; set; }
    public List<string>? FungalSources { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
    public List<string>? References { get; set; }
    public double DataQuality { get; set; } = 1.0;
}

// External API response models
public class FungiDbResponse
{
    public List<FungiDbSpecies>? Species { get; set; }
}

public class FungiDbSpecies
{
    public string Id { get; set; } = string.Empty;
    public string SpeciesName { get; set; } = string.Empty;
    public string? Phylum { get; set; }
    public string? Class { get; set; }
    public string? Order { get; set; }
    public string? Family { get; set; }
    public string? Genus { get; set; }
    public string? Authority { get; set; }
    public List<string>? CommonNames { get; set; }
    public List<string>? Synonyms { get; set; }
    public string? Description { get; set; }
    public string? Habitat { get; set; }
    public string? Distribution { get; set; }
    public object? Morphology { get; set; }
    public object? Ecology { get; set; }
    public List<string>? Images { get; set; }
}

public class INaturalistResponse
{
    public List<INaturalistObservation>? Results { get; set; }
}

public class INaturalistObservation
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ObservedOn { get; set; }
    public string? PlaceGuess { get; set; }
    public string? QualityGrade { get; set; }
    public double? PositionalAccuracy { get; set; }
    public INaturalistLocation? Location { get; set; }
    public INaturalistTaxon? Taxon { get; set; }
    public INaturalistTaxon? CommunityTaxon { get; set; }
    public List<INaturalistPhoto>? Photos { get; set; }
    public List<INaturalistSound>? Sounds { get; set; }
}

public class INaturalistLocation
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class INaturalistTaxon
{
    public string? Name { get; set; }
    public string? PreferredCommonName { get; set; }
    public INaturalistTaxon? Kingdom { get; set; }
    public INaturalistTaxon? Phylum { get; set; }
    public INaturalistTaxon? Class { get; set; }
    public INaturalistTaxon? Order { get; set; }
    public INaturalistTaxon? Family { get; set; }
    public INaturalistTaxon? Genus { get; set; }
}

public class INaturalistPhoto
{
    public string? Url { get; set; }
}

public class INaturalistSound
{
    public string? Url { get; set; }
}

public class MycoBankResponse
{
    public List<MycoBankRecord>? Results { get; set; }
}

public class MycoBankRecord
{
    public string MBNumber { get; set; } = string.Empty;
    public string? EpithetLowerCase { get; set; }
    public string? Genus { get; set; }
    public string? Authors { get; set; }
    public string? Publication { get; set; }
    public int? YearOfPublication { get; set; }
    public string? Status { get; set; }
    public string? Basionym { get; set; }
    public string? TypeSpecimen { get; set; }
    public List<string>? Synonyms { get; set; }
    public List<string>? References { get; set; }
    public MycoBankHigherClassification? HigherClassification { get; set; }
}

public class MycoBankHigherClassification
{
    public string? Phylum { get; set; }
    public string? Class { get; set; }
    public string? Order { get; set; }
    public string? Family { get; set; }
}

public class TaxonomyRecord
{
    public string Id { get; set; } = string.Empty;
    public string? Kingdom { get; set; }
    public string? Phylum { get; set; }
    public string? Class { get; set; }
    public string? Order { get; set; }
    public string? Family { get; set; }
    public string? Genus { get; set; }
    public string? Species { get; set; }
    public string? Authority { get; set; }
    public string? Publication { get; set; }
    public int? PublicationYear { get; set; }
    public List<string> CommonNames { get; set; } = new();
    public List<string> Synonyms { get; set; } = new();
    public string? Description { get; set; }
    public string? Habitat { get; set; }
    public string? Distribution { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class CompoundRecord
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Formula { get; set; }
    public double? MolecularWeight { get; set; }
    public string? SMILES { get; set; }
    public string? InChI { get; set; }
    public string? CASNumber { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? BiologicalActivity { get; set; }
    public List<string> FungalSources { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
    public List<string> References { get; set; } = new();
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
} 