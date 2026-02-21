namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service interface for data export (CSV, JSON, FASTA)
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Export data to CSV format
    /// </summary>
    Task<ExportResult> ExportToCsvAsync(string dataType, ExportQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Export data to JSON format
    /// </summary>
    Task<ExportResult> ExportToJsonAsync(string dataType, ExportQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Export sequences to FASTA format
    /// </summary>
    Task<ExportResult> ExportToFastaAsync(ExportQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get available export data types
    /// </summary>
    IEnumerable<string> GetAvailableDataTypes();
}

/// <summary>
/// Export query parameters
/// </summary>
public class ExportQuery
{
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? DeviceId { get; set; }
    public string? Filter { get; set; }
    public int MaxRecords { get; set; } = 10000;
}

/// <summary>
/// Export result
/// </summary>
public class ExportResult
{
    public string Format { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Filename { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
    public int RecordCount { get; set; }
}
