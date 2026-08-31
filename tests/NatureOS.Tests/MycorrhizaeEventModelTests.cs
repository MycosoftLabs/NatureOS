using Xunit;
using NatureOS.MINDEX.Models;

namespace NatureOS.Tests;

/// <summary>
/// Baseline model tests for MycorrhizaeEvent and related types.
/// Ensures serialization-critical properties are correctly initialised.
/// </summary>
public class MycorrhizaeEventModelTests
{
    [Fact]
    public void MycorrhizaeEvent_HasExpectedDefaults()
    {
        var ev = new MycorrhizaeEvent();
        Assert.Equal(string.Empty, ev.EventId);
        Assert.Equal(string.Empty, ev.SourceDevice);
        Assert.Equal(string.Empty, ev.KingdomDomain);
        Assert.Null(ev.SignalVector);
        Assert.Null(ev.DecodedMeaning);
        Assert.Null(ev.References);
        Assert.Null(ev.Metadata);
    }

    [Fact]
    public void TaxonomicClassification_AllFieldsNullable()
    {
        var tax = new TaxonomicClassification();
        Assert.Null(tax.Kingdom);
        Assert.Null(tax.Phylum);
        Assert.Null(tax.Class);
        Assert.Null(tax.Order);
        Assert.Null(tax.Family);
        Assert.Null(tax.Genus);
        Assert.Null(tax.Species);
        Assert.Null(tax.ScientificName);
    }

    [Fact]
    public void EventMetadata_DefaultQualityScore()
    {
        var meta = new EventMetadata();
        Assert.Null(meta.QualityScore);
        Assert.Null(meta.IngestedAt);
    }

    [Fact]
    public void GeoLocation_DefaultCoordinates()
    {
        var geo = new GeoLocation();
        Assert.Equal(0.0, geo.Latitude);
        Assert.Equal(0.0, geo.Longitude);
        Assert.Null(geo.Elevation);
    }

    [Fact]
    public void DeviceStatus_EnumValues()
    {
        Assert.Equal(0, (int)DeviceStatus.Unknown);
        Assert.Equal(1, (int)DeviceStatus.Online);
        Assert.Equal(2, (int)DeviceStatus.Offline);
        Assert.Equal(3, (int)DeviceStatus.Maintenance);
        Assert.Equal(4, (int)DeviceStatus.Error);
    }

    [Fact]
    public void EventStatistics_DefaultsAreZero()
    {
        var stats = new NatureOS.CoreApi.Services.EventStatistics();
        Assert.Equal(0, stats.TotalEvents);
        Assert.Equal(0, stats.TodayCount);
        Assert.Equal(0.0, stats.AveragePerHour);
        Assert.Empty(stats.EventsByDomain);
    }

    [Fact]
    public void DeviceStatistics_TotalCountAlias()
    {
        var stats = new NatureOS.CoreApi.Services.DeviceStatistics { TotalDevices = 42 };
        Assert.Equal(42, stats.TotalCount);
    }
}
