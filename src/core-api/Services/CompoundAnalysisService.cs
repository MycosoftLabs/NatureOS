namespace NatureOS.CoreApi.Services;

/// <summary>
/// Compound analysis service with MATLAB chemoinformatics integration.
/// Uses MatlabIntegrationService for molecular descriptor calculation when available.
/// </summary>
public class CompoundAnalysisService : ICompoundAnalysisService
{
    private readonly IMatlabIntegrationService _matlabService;
    private readonly ILogger<CompoundAnalysisService> _logger;

    public CompoundAnalysisService(IMatlabIntegrationService matlabService, ILogger<CompoundAnalysisService> logger)
    {
        _matlabService = matlabService;
        _logger = logger;
    }

    public async Task<MolecularDescriptors> CalculateDescriptorsAsync(string smiles, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(smiles))
            return new MolecularDescriptors { Smiles = "" };

        try
        {
            var result = await _matlabService.ExecuteAnalysisAsync("calculateMolecularDescriptors", new object[] { smiles }, cancellationToken);
            if (result.Success)
            {
                return new MolecularDescriptors
                {
                    Smiles = smiles,
                    MolecularWeight = result.GetValue<double>("molecularWeight"),
                    LogP = result.GetValue<double>("logP"),
                    HeavyAtomCount = result.GetValue<int>("heavyAtomCount"),
                    Fingerprint = result.Get<double[]>("fingerprint"),
                    CustomDescriptors = result.Get<Dictionary<string, double>>("customDescriptors")
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MATLAB descriptor calculation failed for {Smiles}", smiles);
        }

        return new MolecularDescriptors
        {
            Smiles = smiles,
            MolecularWeight = 0,
            LogP = 0,
            HeavyAtomCount = 0
        };
    }

    public async Task<SimilarityResult[]> FindSimilarCompoundsAsync(string smiles, int topK = 10, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(smiles))
            return Array.Empty<SimilarityResult>();

        try
        {
            var result = await _matlabService.ExecuteAnalysisAsync("clusterCompounds", new object[] { smiles, topK }, cancellationToken);
            if (result.Success && result.Outputs.TryGetValue("similar", out var simObj) && simObj is IEnumerable<object> simList)
            {
                var list = new List<SimilarityResult>();
                foreach (var s in simList.Take(topK))
                {
                    if (s is Dictionary<string, object> d)
                    {
                        list.Add(new SimilarityResult
                        {
                            CompoundId = d.TryGetValue("id", out var id) ? id?.ToString() ?? "" : "",
                            Smiles = d.TryGetValue("smiles", out var sm) ? sm?.ToString() : null,
                            Similarity = d.TryGetValue("similarity", out var sim) ? Convert.ToDouble(sim) : 0
                        });
                    }
                }
                return list.ToArray();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MATLAB similarity search failed");
        }

        return Array.Empty<SimilarityResult>();
    }

    public async Task<ActivityPrediction> PredictBioactivityAsync(string smiles, string targetId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(smiles))
            return new ActivityPrediction { Smiles = "", TargetId = targetId };

        try
        {
            var result = await _matlabService.ExecuteAnalysisAsync("predictActivity", new object[] { smiles, targetId }, cancellationToken);
            if (result.Success)
            {
                return new ActivityPrediction
                {
                    Smiles = smiles,
                    TargetId = targetId,
                    Score = result.GetValue<double>("score"),
                    PredictedActivity = result.Get<string>("predictedActivity")
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MATLAB activity prediction failed");
        }

        return new ActivityPrediction { Smiles = smiles, TargetId = targetId, Score = 0 };
    }
}
