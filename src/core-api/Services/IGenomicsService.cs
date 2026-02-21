namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service for genomics/sequence analysis with MATLAB Bioinformatics Toolbox integration
/// </summary>
public interface IGenomicsService
{
    /// <summary>
    /// Align sequences (e.g., ITS barcode)
    /// </summary>
    Task<AlignmentResult> AlignSequencesAsync(string[] sequences, CancellationToken cancellationToken = default);

    /// <summary>
    /// Build phylogenetic tree from aligned sequences
    /// </summary>
    Task<PhylogenyResult> BuildPhylogenyAsync(string alignment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect variants from aligned reads
    /// </summary>
    Task<VariantResult[]> DetectVariantsAsync(string reference, string[] reads, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze differential expression (RNA-seq)
    /// </summary>
    Task<ExpressionResult> AnalyzeExpressionAsync(Dictionary<string, double[]> countsByGene, string[] conditions, CancellationToken cancellationToken = default);
}

/// <summary>
/// Sequence alignment result
/// </summary>
public class AlignmentResult
{
    public string[] AlignedSequences { get; set; } = Array.Empty<string>();
    public double Score { get; set; }
    public string? Consensus { get; set; }
}

/// <summary>
/// Phylogenetic tree result
/// </summary>
public class PhylogenyResult
{
    public string? NewickTree { get; set; }
    public Dictionary<string, double>? BranchLengths { get; set; }
}

/// <summary>
/// Variant call result
/// </summary>
public class VariantResult
{
    public int Position { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Alternate { get; set; } = string.Empty;
    public double Frequency { get; set; }
}

/// <summary>
/// Differential expression result
/// </summary>
public class ExpressionResult
{
    public Dictionary<string, double> Log2FoldChange { get; set; } = new();
    public Dictionary<string, double> PValues { get; set; } = new();
    public int SignificantCount { get; set; }
}
