using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;
using NatureOS.MINDEX.Models;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Json = System.Text.Json.JsonSerializer;

namespace NatureOS.CoreApi.Services;

/// <summary>
/// Read the three event shapes produced by the existing DTO serializers. No data migration.
/// The provider must support ORDER BY on timestamp and Timestamp. A continuation represents
/// consumed positions, not a snapshot; unread heads are fetched again on the next request.
/// </summary>
internal static class FungaEventReader
{
    private const int MaximumTokenLength = 65536;
    private static readonly string[] Domains = ["kingdom_domain", "kingdomDomain", "KingdomDomain"];
    private static readonly CamelCasePropertyNamesContractResolver Camel = new();

    public static async Task<PagedResult<MycorrhizaeEvent>> ReadAsync(
        Container container, FungaQuery query, CancellationToken cancellationToken)
    {
        query.ValidateEnvironmentalRanges();
        var definitions = Enumerable.Range(0, 3).Select(i => BuildQuery(query, i)).ToArray();
        var fingerprint = Fingerprint(query);
        var states = Decode(query.ContinuationToken, fingerprint);
        var heads = new Head?[3];
        var items = new List<MycorrhizaeEvent>();
        var readsRemaining = 3 * (query.PageSize + 1) + 32;
        var ascending = query.SortOrder == "timestamp_asc";

        async Task Peek(int i)
        {
            while (heads[i] == null && !states[i].Done)
            {
                if (--readsRemaining < 0)
                    throw new FungaDocumentSchemaException("Provider page budget exhausted");
                using var iterator = container.GetItemQueryIterator<RawDocument>(definitions[i],
                    continuationToken: states[i].Token,
                    requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
                var response = await iterator.ReadNextAsync(cancellationToken);
                if (response.Count > 1)
                    throw new FungaDocumentSchemaException("Provider exceeded requested page size");
                if (response.Count == 0)
                {
                    states[i] = new Cursor(response.ContinuationToken, response.ContinuationToken == null);
                    continue;
                }
                var document = response.Single().Document;
                var timestamp = document[i == 2 ? "Timestamp" : "timestamp"];
                if (timestamp?.Type != JTokenType.String)
                    throw new FungaDocumentSchemaException("Event timestamp must be an ISO8601 string");
                var model = Materialize(document);
                heads[i] = new Head(model, timestamp.Value<string>()!, response.ContinuationToken);
            }
        }

        while (items.Count < query.PageSize)
        {
            for (var i = 0; i < 3; i++) await Peek(i);
            var selected = -1;
            for (var i = 0; i < 3; i++)
            {
                if (heads[i] == null) continue;
                if (selected < 0) { selected = i; continue; }
                // Match Cosmos ORDER BY string order, including differing ISO8601 fractional precision.
                // Chronological normalization here would violate the ordering of each provider stream.
                var comparison = StringComparer.Ordinal.Compare(heads[i]!.SortKey, heads[selected]!.SortKey);
                if (ascending ? comparison < 0 : comparison > 0) selected = i;
                // Equal timestamps retain fixed shape order; provider order breaks ties within a shape.
            }
            if (selected < 0) break;
            var head = heads[selected]!;
            items.Add(head.Event);
            states[selected] = new Cursor(head.NextToken, head.NextToken == null);
            heads[selected] = null;
        }
        var more = states.Any(x => !x.Done);
        return new PagedResult<MycorrhizaeEvent>
        {
            Items = items,
            HasMore = more,
            ContinuationToken = more ? Encode(new Envelope(1, fingerprint, states)) : null
        };
    }

    private static QueryDefinition BuildQuery(FungaQuery q, int shape)
    {
        var legacy = shape == 2;
        var prefix = legacy ? "References" : "references";
        var environment = prefix + (legacy ? ".Environment." : ".environment.");
        var taxonomy = prefix + (legacy ? ".Taxonomy.Phylum" : ".taxonomy.phylum");
        var timestamp = legacy ? "Timestamp" : "timestamp";
        var source = shape == 0 ? "source_device" : legacy ? "SourceDevice" : "sourceDevice";
        var decoded = shape == 0 ? "decoded_meaning" : legacy ? "DecodedMeaning" : "decodedMeaning";
        var conditions = new List<string>();
        var parameters = new List<(string, object)>();
        // Presence priority makes multiple equal aliases belong to exactly one stream.
        for (var i = 0; i < shape; i++) conditions.Add($"NOT IS_DEFINED(c.{Domains[i]})");
        conditions.Add($"IS_DEFINED(c.{Domains[shape]})");
        conditions.Add("(" + string.Join(" OR ", Domains.Select(d => $"c.{d} LIKE 'FUNGA%'")) + ")");
        void Equal(string path, string name, string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            conditions.Add($"c.{path} = @{name}"); parameters.Add(($"@{name}", value));
        }
        void Range(string path, string name, double min, double max)
        {
            conditions.Add($"IS_NUMBER(c.{path}) AND c.{path} >= @{name}Min AND c.{path} <= @{name}Max");
            parameters.Add(($"@{name}Min", min)); parameters.Add(($"@{name}Max", max));
        }
        Equal(source, "sourceDevice", q.SourceDevice);
        Equal(taxonomy, "phylum", q.Phylum);
        Equal(environment + (legacy ? "Substrate" : "substrate"), "substrate", q.SubstrateType);
        if (q.TemperatureRange != null)
            Range(environment + (legacy ? "Temperature" : "temperature"), "temp", q.TemperatureRange.Min, q.TemperatureRange.Max);
        if (q.HumidityRange != null)
            Range(environment + (legacy ? "Humidity" : "humidity"), "humidity", q.HumidityRange.Min, q.HumidityRange.Max);
        if (q.pHRange != null)
            Range(environment + (shape == 0 ? "ph" : "pH"), "ph", q.pHRange.Min, q.pHRange.Max);
        if (q.StartTime.HasValue)
        {
            conditions.Add($"c.{timestamp} >= @startTime"); parameters.Add(("@startTime", q.StartTime.Value));
        }
        if (q.EndTime.HasValue)
        {
            conditions.Add($"c.{timestamp} <= @endTime"); parameters.Add(("@endTime", q.EndTime.Value));
        }
        if (q.MycorrhizalOnly == true)
        {
            var path = decoded + (legacy ? ".Annotations." : ".annotations.") + "mycorrhizal_type";
            conditions.Add($"IS_DEFINED(c.{path}) AND NOT IS_NULL(c.{path})");
        }
        var direction = q.SortOrder == "timestamp_asc" ? "ASC" : "DESC";
        var definition = new QueryDefinition($"SELECT * FROM c WHERE {string.Join(" AND ", conditions)} ORDER BY c.{timestamp} {direction}");
        foreach (var (name, value) in parameters) definition.WithParameter(name, value);
        return definition;
    }

    private static MycorrhizaeEvent Materialize(JObject document)
    {
        try
        {
            var normalized = (JObject)Normalize(document, typeof(MycorrhizaeEvent));
            if (normalized["timestamp"] == null || normalized["timestamp"]!.Type == JTokenType.Null)
                throw new FungaDocumentSchemaException("Event timestamp is missing");
            return Json.Deserialize<MycorrhizaeEvent>(normalized.ToString(Newtonsoft.Json.Formatting.None))
                ?? throw new FungaDocumentSchemaException("Event is null");
        }
        catch (System.Text.Json.JsonException)
        {
            throw new FungaDocumentSchemaException("Event fields do not match the DTO");
        }
    }

    private static JToken Normalize(JToken value, Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (value.Type == JTokenType.Null || type.Namespace != typeof(MycorrhizaeEvent).Namespace)
            return value.DeepClone(); // Arbitrary dictionary/payload keys are never renamed.
        if (value is not JObject obj) throw new FungaDocumentSchemaException("Expected an event object");
        var result = new JObject();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var canonical = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
            var camel = Camel.GetResolvedPropertyName(property.Name);
            var aliases = new[] { canonical, property.Name, camel }.Distinct(StringComparer.Ordinal);
            JToken? selected = null;
            foreach (var alias in aliases)
            {
                if (!obj.TryGetValue(alias, StringComparison.Ordinal, out var raw)) continue;
                var candidate = Normalize(raw, property.PropertyType);
                if (selected != null && !Equivalent(selected, candidate))
                    throw new FungaDocumentSchemaException($"Conflicting aliases for {canonical}");
                selected = candidate;
            }
            if (selected != null) result[canonical] = selected;
        }
        return result;
    }

    private static bool Equivalent(JToken a, JToken b)
    {
        if (a.Type is JTokenType.Integer or JTokenType.Float && b.Type is JTokenType.Integer or JTokenType.Float)
            return a.Value<double>().Equals(b.Value<double>());
        if (a is JObject x && b is JObject y)
            return x.Count == y.Count && x.Properties().All(p => y.TryGetValue(p.Name, StringComparison.Ordinal, out var v) && Equivalent(p.Value, v));
        return JToken.DeepEquals(a, b);
    }

    private static string Fingerprint(FungaQuery q)
    {
        var input = Json.Serialize(new { q.SourceDevice, q.Phylum, q.SubstrateType, q.TemperatureRange,
            q.HumidityRange, q.pHRange, q.StartTime, q.EndTime, MycorrhizalOnly = q.MycorrhizalOnly == true,
            Ascending = q.SortOrder == "timestamp_asc" });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }

    private static Cursor[] Decode(string? token, string fingerprint)
    {
        if (token == null) return [new(null, false), new(null, false), new(null, false)];
        try
        {
            if (token.Length == 0 || token.Length > MaximumTokenLength || token.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
                throw new FormatException();
            var base64 = token.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
            var envelope = Json.Deserialize<Envelope>(Convert.FromBase64String(base64));
            if (envelope == null || envelope.Version != 1 || envelope.Fingerprint != fingerprint ||
                envelope.Streams == null || envelope.Streams.Length != 3 ||
                envelope.Streams.Any(c => c == null || c.Token?.Length == 0 || c.Done && c.Token != null))
                throw new FormatException();
            return envelope.Streams;
        }
        catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException)
        {
            throw new FungaQueryValidationException("Invalid or incompatible continuation token; restart the query");
        }
    }

    private static string Encode(Envelope envelope)
    {
        var encoded = Convert.ToBase64String(Json.SerializeToUtf8Bytes(envelope)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        if (encoded.Length > MaximumTokenLength)
            throw new FungaDocumentSchemaException("Provider continuation exceeds bounded envelope size");
        return encoded;
    }

    private sealed record Cursor(string? Token, bool Done);
    private sealed record Envelope(int Version, string Fingerprint, Cursor[] Streams);
    private sealed record Head(MycorrhizaeEvent Event, string SortKey, string? NextToken);

    [Newtonsoft.Json.JsonConverter(typeof(RawDocumentConverter))]
    public sealed record RawDocument(JObject Document);

    /// <summary>Preserve exact SQL string sort keys before Newtonsoft can coerce dates.</summary>
    public sealed class RawDocumentConverter : Newtonsoft.Json.JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(RawDocument);
        public override bool CanWrite => false;
        public override object ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType,
            object? existingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            var previous = reader.DateParseHandling;
            try
            {
                reader.DateParseHandling = Newtonsoft.Json.DateParseHandling.None;
                return new RawDocument(JObject.Load(reader));
            }
            finally { reader.DateParseHandling = previous; }
        }
        public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object? value,
            Newtonsoft.Json.JsonSerializer serializer) => throw new NotSupportedException();
    }
}
