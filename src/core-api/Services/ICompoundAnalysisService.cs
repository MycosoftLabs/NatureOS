namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service for compound/molecular analysis with MATLAB chemoinformatics integration
/// </summary>
public interface ICompoundAnalysisService
{
    /// <summary>
    /// Calculate molecular descriptors from SMILES
    /// </summary>
    Task<MolecularDescriptors> CalculateDescriptorsAsync(string smiles, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find similar compounds by structure
    /// </summary>
    Task<SimilarityResult[]> FindSimilarCompoundsAsync(string smiles, int topK = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Predict bioactivity for a target
    /// </summary>
    Task<ActivityPrediction> PredictBioactivityAsync(string smiles, string targetId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Molecular descriptors
/// </summary>
public class MolecularDescriptors
{
    public string Smiles { get; set; } = string.Empty;
    public double MolecularWeight { get; set; }
    public double LogP { get; set; }
    public int HeavyAtomCount { get; set; }
    public double[]? Fingerprint { get; set; }
    public Dictionary<string, double>? CustomDescriptors { get; set; }
}

/// <summary>
/// Similar compound result
/// </summary>
public class SimilarityResult
{
    public string CompoundId { get; set; } = string.Empty;
    public string? Smiles { get; set; }
    public double Similarity { get; set; }
}

/// <summary>
/// Bioactivity prediction
/// </summary>
public class ActivityPrediction
{
    public string Smiles { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public double Score { get; set; }
    public string? PredictedActivity { get; set; }
}
