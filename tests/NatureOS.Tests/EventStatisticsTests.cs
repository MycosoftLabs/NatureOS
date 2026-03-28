using FluentAssertions;
using NatureOS.CoreApi.Services;
using Xunit;

namespace NatureOS.Tests;

public class EventStatisticsTests
{
    [Fact]
    public void NewStatistics_ShouldHaveZeroDefaults()
    {
        var stats = new EventStatistics();

        stats.TotalEvents.Should().Be(0);
        stats.TodayCount.Should().Be(0);
        stats.AveragePerHour.Should().Be(0);
        stats.AveragePerDay.Should().Be(0);
        stats.UniqueSpeciesCount.Should().Be(0);
    }

    [Fact]
    public void TotalCount_ShouldAliasToTotalEvents()
    {
        var stats = new EventStatistics { TotalEvents = 42 };

        stats.TotalCount.Should().Be(42);
    }

    [Fact]
    public void EventsByDomain_ShouldDefaultToEmptyDictionary()
    {
        var stats = new EventStatistics();

        stats.EventsByDomain.Should().NotBeNull();
        stats.EventsByDomain.Should().BeEmpty();
    }

    [Fact]
    public void EventsByDevice_ShouldDefaultToEmptyDictionary()
    {
        var stats = new EventStatistics();

        stats.EventsByDevice.Should().NotBeNull();
        stats.EventsByDevice.Should().BeEmpty();
    }

    [Fact]
    public void AveragePerHour_CanBeComputedFromTimeRange()
    {
        var stats = new EventStatistics
        {
            TotalEvents = 240,
            TimeRange = new DateTimeRange
            {
                Start = DateTime.UtcNow.AddHours(-24),
                End = DateTime.UtcNow
            }
        };

        var totalSpan = stats.TimeRange.End - stats.TimeRange.Start;
        var avgPerHour = stats.TotalEvents / totalSpan.TotalHours;

        avgPerHour.Should().Be(10.0);
    }

    [Fact]
    public void AveragePerDay_CanBeComputedFromTimeRange()
    {
        var stats = new EventStatistics
        {
            TotalEvents = 700,
            TimeRange = new DateTimeRange
            {
                Start = DateTime.UtcNow.AddDays(-7),
                End = DateTime.UtcNow
            }
        };

        var totalSpan = stats.TimeRange.End - stats.TimeRange.Start;
        var avgPerDay = stats.TotalEvents / totalSpan.TotalDays;

        avgPerDay.Should().Be(100.0);
    }
}
