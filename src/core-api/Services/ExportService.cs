using System.Globalization;
using System.Text.Json;
using NatureOS.MINDEX.Models;

namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service for data export (CSV, JSON, FASTA)
/// </summary>
public class ExportService : IExportService
{
    private readonly IEventService _eventService;
    private readonly ILogger<ExportService> _logger;

    public ExportService(IEventService eventService, ILogger<ExportService> logger)
    {
        _eventService = eventService;
        _logger = logger;
    }

    public async Task<ExportResult> ExportToCsvAsync(string dataType, ExportQuery query, CancellationToken cancellationToken = default)
    {
        var events = await FetchEventsAsync(query, cancellationToken);
        var lines = new List<string> { "EventId,SourceDevice,KingdomDomain,Phylum,SpeciesName,Timestamp,Latitude,Longitude" };
        foreach (var e in events)
        {
            var phylum = e.References?.Taxonomy?.Phylum ?? "";
            var species = e.References?.Taxonomy?.Species ?? e.References?.Taxonomy?.ScientificName ?? "";
            var lat = e.References?.Location?.Latitude.ToString(CultureInfo.InvariantCulture) ?? "";
            var lon = e.References?.Location?.Longitude.ToString(CultureInfo.InvariantCulture) ?? "";
            lines.Add($"{EscapeCsv(e.EventId)},{EscapeCsv(e.SourceDevice)},{EscapeCsv(e.KingdomDomain)},{EscapeCsv(phylum)},{EscapeCsv(species)},{e.Timestamp:O},{lat},{lon}");
        }
        return new ExportResult
        {
            Format = "csv",
            Content = string.Join("\n", lines),
            Filename = $"natureos-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv",
            ContentType = "text/csv",
            RecordCount = events.Count
        };
    }

    public async Task<ExportResult> ExportToJsonAsync(string dataType, ExportQuery query, CancellationToken cancellationToken = default)
    {
        var events = await FetchEventsAsync(query, cancellationToken);
        var json = JsonSerializer.Serialize(events, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return new ExportResult
        {
            Format = "json",
            Content = json,
            Filename = $"natureos-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json",
            ContentType = "application/json",
            RecordCount = events.Count
        };
    }

    public async Task<ExportResult> ExportToFastaAsync(ExportQuery query, CancellationToken cancellationToken = default)
    {
        var events = await FetchEventsAsync(query, cancellationToken);
        var lines = new List<string>();
        var idx = 0;
        foreach (var e in events)
        {
            var seq = e.References?.Taxonomy?.ScientificName ?? e.References?.Taxonomy?.Species ?? "";
            if (!string.IsNullOrEmpty(seq))
            {
                idx++;
                lines.Add($">event_{e.EventId}_{idx} {seq}");
                lines.Add(seq.Replace("\n", " ").Replace("\r", ""));
            }
        }
        return new ExportResult
        {
            Format = "fasta",
            Content = string.Join("\n", lines),
            Filename = $"natureos-sequences-{DateTime.UtcNow:yyyyMMdd-HHmmss}.fasta",
            ContentType = "text/plain",
            RecordCount = lines.Count / 2
        };
    }

    public IEnumerable<string> GetAvailableDataTypes() => new[] { "events", "observations", "devices" };

    private async Task<List<MycorrhizaeEvent>> FetchEventsAsync(ExportQuery query, CancellationToken cancellationToken)
    {
        var eq = new EventQuery
        {
            StartTime = query.StartTime ?? DateTime.UtcNow.AddDays(-7),
            EndTime = query.EndTime ?? DateTime.UtcNow,
            SourceDevice = query.DeviceId,
            PageSize = Math.Min(query.MaxRecords, 10000)
        };
        var result = await _eventService.GetEventsAsync(eq, cancellationToken);
        return result.Items.ToList();
    }

    private static string EscapeCsv(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
