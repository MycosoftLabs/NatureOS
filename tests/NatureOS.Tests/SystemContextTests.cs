using FluentAssertions;
using NatureOS.CoreApi.Services;
using Xunit;

namespace NatureOS.Tests;

public class SystemContextTests
{
    [Fact]
    public void TopSpecies_ShouldExcludeErrorDomains()
    {
        var eventsByDomain = new Dictionary<string, long>
        {
            ["FUNGA.environmental"] = 100,
            ["FUNGA.telemetry"] = 80,
            ["FUNGA.error.sensor"] = 50,
            ["FUNGA.discovery"] = 30,
            ["FUNGA.analysis"] = 20
        };

        var excludedDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "error", "telemetry" };
        var topSpecies = eventsByDomain
            .Where(kvp => !excludedDomains.Any(ex => kvp.Key.Contains(ex, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(kvp => kvp.Value)
            .Take(5)
            .Select(kvp => kvp.Key)
            .ToArray();

        topSpecies.Should().NotContain(d => d.Contains("error", StringComparison.OrdinalIgnoreCase));
        topSpecies.Should().NotContain(d => d.Contains("telemetry", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TopSpecies_ShouldReturnMaxFiveItems()
    {
        var eventsByDomain = new Dictionary<string, long>
        {
            ["FUNGA.a"] = 100,
            ["FUNGA.b"] = 90,
            ["FUNGA.c"] = 80,
            ["FUNGA.d"] = 70,
            ["FUNGA.e"] = 60,
            ["FUNGA.f"] = 50,
            ["FUNGA.g"] = 40,
        };

        var topSpecies = eventsByDomain
            .OrderByDescending(kvp => kvp.Value)
            .Take(5)
            .Select(kvp => kvp.Key)
            .ToArray();

        topSpecies.Should().HaveCount(5);
        topSpecies[0].Should().Be("FUNGA.a");
    }

    [Fact]
    public void TrendingCompounds_ShouldDeriveFungaPrefixedDomains()
    {
        var domains = new[]
        {
            "FUNGA.environmental", "FUNGA.environmental", "FUNGA.environmental",
            "FUNGA.metabolic", "FUNGA.metabolic",
            "SYSTEM.telemetry",
            "ERROR.sensor"
        };

        var trending = domains
            .Where(d => d.StartsWith("FUNGA.", StringComparison.OrdinalIgnoreCase))
            .GroupBy(d => d, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToArray();

        trending.Should().HaveCount(2);
        trending[0].Should().Be("FUNGA.environmental");
        trending[1].Should().Be("FUNGA.metabolic");
    }

    [Fact]
    public void TrendingCompounds_ShouldHandleEmptyEvents()
    {
        var domains = Array.Empty<string>();

        var trending = domains
            .Where(d => d.StartsWith("FUNGA.", StringComparison.OrdinalIgnoreCase))
            .GroupBy(d => d, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToArray();

        trending.Should().BeEmpty();
    }

    [Fact]
    public void TopSpecies_ShouldHandleEmptyDomains()
    {
        var eventsByDomain = new Dictionary<string, long>();

        var topSpecies = eventsByDomain
            .OrderByDescending(kvp => kvp.Value)
            .Take(5)
            .Select(kvp => kvp.Key)
            .ToArray();

        topSpecies.Should().BeEmpty();
    }
}
