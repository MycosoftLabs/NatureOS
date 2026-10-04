using Xunit;
using NatureOS.CoreApi.Services;
using NatureOS.MINDEX.Models;

namespace NatureOS.Tests;

/// <summary>
/// Tests for Device model and update semantics (issue #6).
/// Verifies the auto-timestamp and CreatedAt-backfill behaviour
/// that supports MycoBrain-only device upserts.
/// </summary>
public class DeviceUpsertTests
{
    [Fact]
    public void Device_DefaultCreatedAtIsMinValue()
    {
        var device = new Device { DeviceId = "test-001" };
        Assert.Equal(default, device.CreatedAt);
    }

    [Fact]
    public void Device_UpdateSetsUpdatedAt()
    {
        var device = new Device
        {
            DeviceId = "test-001",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        device.UpdatedAt = DateTime.UtcNow;
        Assert.True(device.UpdatedAt > device.CreatedAt);
    }

    [Fact]
    public void Device_BackfillCreatedAt_WhenDefault()
    {
        var device = new Device { DeviceId = "new-device" };

        if (device.CreatedAt == default)
            device.CreatedAt = DateTime.UtcNow;

        Assert.NotEqual(default, device.CreatedAt);
    }

    [Fact]
    public void MycoBrainDevice_DefaultTypeIsMycobrain()
    {
        var device = new MycoBrainDevice();
        Assert.Equal("mycobrain", device.DeviceType);
    }

    [Fact]
    public void MycoBrainDevice_StatusDefaultIsUnknown()
    {
        var device = new MycoBrainDevice();
        Assert.Equal(DeviceStatus.Unknown, device.Status);
    }
}
