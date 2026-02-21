namespace NatureOS.CoreApi.Services;

/// <summary>
/// Genomics service with MATLAB sequence analysis integration
/// </summary>
public class GenomicsService : IGenomicsService
{
    private readonly IMatlabIntegrationService _matlabService;
    private readonly ILogger<GenomicsService> _logger;

    public GenomicsService(IMatlabIntegrationService matlabService, ILogger<GenomicsService> logger)
    {
        _matlabService = matlabService;
        _logger = logger;
    }

    public async Task<AlignmentResult> AlignSequencesAsync(string[] sequences, CancellationToken cancellationToken = default)
    {
        if (sequences == null || sequences.Length < 2)
            return new AlignmentResult { AlignedSequences = sequences ?? Array.Empty<string>() };

        try
        {
            var result = await _matlabService.ExecuteAnalysisAsync("alignSequences", new object[] { sequences }, cancellationToken);
            if (result.Success)
            {
                var aligned = result.Get<string[]>("alignedSequences");
                return new AlignmentResult
                {
                    AlignedSequences = aligned ?? sequences,
                    Score = result.GetValue<double>("score"),
                    Consensus = result.Get<string>("consensus")
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MATLAB sequence alignment failed");
        }

        return new AlignmentResult { AlignedSequences = sequences };
    }

    public async Task<PhylogenyResult> BuildPhylogenyAsync(string alignment, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(alignment))
            return new PhylogenyResult();

        try
        {
            var result = await _matlabService.ExecuteAnalysisAsync("buildPhylogeny", new object[] { alignment }, cancellationToken);
            if (result.Success)
            {
                return new PhylogenyResult
                {
                    NewickTree = result.Get<string>("newickTree"),
                    BranchLengths = result.Get<Dictionary<string, double>>("branchLengths")
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MATLAB phylogeny build failed");
        }

        return new PhylogenyResult();
    }

    public async Task<VariantResult[]> DetectVariantsAsync(string reference, string[] reads, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reference) || reads == null || reads.Length == 0)
            return Array.Empty<VariantResult>();

        try
        {
            var result = await _matlabService.ExecuteAnalysisAsync("detectVariants", new object[] { reference, reads }, cancellationToken);
            if (result.Success && result.Outputs.TryGetValue("variants", out var v) && v is object[] arr)
            {
                return arr.Select(x =>
                {
                    if (x is Dictionary<string, object> d)
                        return new VariantResult
                        {
                            Position = Convert.ToInt32(d.TryGetValue("position", out var p) ? p : 0),
                            Reference = d.TryGetValue("reference", out var r) ? r?.ToString() ?? "" : "",
                            Alternate = d.TryGetValue("alternate", out var a) ? a?.ToString() ?? "" : "",
                            Frequency = d.TryGetValue("frequency", out var f) ? Convert.ToDouble(f) : 0
                        };
                    return new VariantResult();
                }).ToArray();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MATLAB variant detection failed");
        }

        return Array.Empty<VariantResult>();
    }

    public async Task<ExpressionResult> AnalyzeExpressionAsync(Dictionary<string, double[]> countsByGene, string[] conditions, CancellationToken cancellationToken = default)
    {
        if (countsByGene == null || countsByGene.Count == 0)
            return new ExpressionResult();

        try
        {
            var result = await _matlabService.ExecuteAnalysisAsync("analyzeExpression", new object[] { countsByGene, conditions }, cancellationToken);
            if (result.Success)
            {
                return new ExpressionResult
                {
                    Log2FoldChange = result.Get<Dictionary<string, double>>("log2FoldChange") ?? new(),
                    PValues = result.Get<Dictionary<string, double>>("pValues") ?? new(),
                    SignificantCount = result.GetValue<int>("significantCount")
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MATLAB expression analysis failed");
        }

        return new ExpressionResult();
    }
}
