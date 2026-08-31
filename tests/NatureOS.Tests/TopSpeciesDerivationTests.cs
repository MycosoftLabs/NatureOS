using Xunit;
using NatureOS.MINDEX.Models;

namespace NatureOS.Tests;

/// <summary>
/// Tests that TopSpecies derivation extracts species from taxonomy fields
/// rather than approximating from kingdom_domain strings (issue #7).
/// These are pure-logic tests that validate the extraction algorithm
/// independently of the controller/service wiring.
/// </summary>
public class TopSpeciesDerivationTests
{
    [Fact]
    public void ExtractSpecies_PrefersTaxonomySpeciesField()
    {
        var events = new[]
        {
            MakeEvent(species: "Agaricus bisporus"),
            MakeEvent(species: "Pleurotus ostreatus"),
            MakeEvent(species: "Agaricus bisporus"),
        };

        var result = DeriveTopSpecies(events);

        Assert.Equal(2, result.Length);
        Assert.Equal("Agaricus bisporus", result[0]);
        Assert.Equal("Pleurotus ostreatus", result[1]);
    }

    [Fact]
    public void ExtractSpecies_FallsBackToScientificName()
    {
        var events = new[]
        {
            MakeEvent(species: null, scientificName: "Trametes versicolor"),
            MakeEvent(species: null, scientificName: "Trametes versicolor"),
        };

        var result = DeriveTopSpecies(events);

        Assert.Single(result);
        Assert.Equal("Trametes versicolor", result[0]);
    }

    [Fact]
    public void ExtractSpecies_IgnoresUnknownAndNull()
    {
        var events = new[]
        {
            MakeEvent(species: "unknown"),
            MakeEvent(species: null, scientificName: null),
            MakeEvent(species: ""),
            MakeEvent(species: "Ganoderma lucidum"),
        };

        var result = DeriveTopSpecies(events);

        Assert.Single(result);
        Assert.Equal("Ganoderma lucidum", result[0]);
    }

    [Fact]
    public void ExtractSpecies_OrdersByFrequencyDescending()
    {
        var events = new[]
        {
            MakeEvent(species: "A"),
            MakeEvent(species: "B"),
            MakeEvent(species: "B"),
            MakeEvent(species: "C"),
            MakeEvent(species: "C"),
            MakeEvent(species: "C"),
        };

        var result = DeriveTopSpecies(events);

        Assert.Equal(3, result.Length);
        Assert.Equal("C", result[0]);
        Assert.Equal("B", result[1]);
        Assert.Equal("A", result[2]);
    }

    [Fact]
    public void ExtractSpecies_CapsAtTen()
    {
        var events = Enumerable.Range(1, 15)
            .Select(i => MakeEvent(species: $"Species_{i}"))
            .ToArray();

        var result = DeriveTopSpecies(events);

        Assert.Equal(10, result.Length);
    }

    [Fact]
    public void ExtractSpecies_EmptyInput_ReturnsEmpty()
    {
        var result = DeriveTopSpecies(Array.Empty<MycorrhizaeEvent>());
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractSpecies_NoTaxonomyAtAll_ReturnsEmpty()
    {
        var events = new[]
        {
            new MycorrhizaeEvent
            {
                EventId = "e1",
                SourceDevice = "dev1",
                KingdomDomain = "FUNGA.telemetry",
                Timestamp = DateTime.UtcNow
            },
        };

        var result = DeriveTopSpecies(events);
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractSpecies_CaseInsensitiveGrouping()
    {
        var events = new[]
        {
            MakeEvent(species: "Agaricus bisporus"),
            MakeEvent(species: "agaricus bisporus"),
            MakeEvent(species: "AGARICUS BISPORUS"),
        };

        var result = DeriveTopSpecies(events);

        Assert.Single(result);
    }

    /// <summary>
    /// The same derivation logic used in MycosoftController.DeriveTopSpeciesAsync,
    /// extracted here as a pure function for unit testing.
    /// </summary>
    private static string[] DeriveTopSpecies(IEnumerable<MycorrhizaeEvent> events)
    {
        return events
            .Select(e =>
                e.References?.Taxonomy?.Species
                ?? e.References?.Taxonomy?.ScientificName
                ?? null)
            .Where(s => !string.IsNullOrWhiteSpace(s) &&
                        !string.Equals(s, "unknown", StringComparison.OrdinalIgnoreCase))
            .GroupBy(s => s!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToArray();
    }

    private static MycorrhizaeEvent MakeEvent(
        string? species = null,
        string? scientificName = null)
    {
        return new MycorrhizaeEvent
        {
            EventId = Guid.NewGuid().ToString(),
            SourceDevice = "test-device",
            KingdomDomain = "FUNGA.observation",
            Timestamp = DateTime.UtcNow,
            References = new EventReferences
            {
                Taxonomy = new TaxonomicClassification
                {
                    Species = species,
                    ScientificName = scientificName
                }
            }
        };
    }
}
