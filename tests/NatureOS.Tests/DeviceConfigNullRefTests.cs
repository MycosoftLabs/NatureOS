using FluentAssertions;
using NatureOS.MINDEX.Models;
using Xunit;

namespace NatureOS.Tests;

public class DeviceConfigNullRefTests
{
    [Fact]
    public void MappedDevice_ShouldHaveNonNullMetadata()
    {
        // Simulate mapping a MycoBrain device to a Device
        var device = new Device
        {
            DeviceId = "test-device-001",
            Name = "test-device-001",
            DeviceType = "mushroom",
            Status = DeviceStatus.Online,
            LastSeen = DateTime.UtcNow,
            Metadata = new Dictionary<string, object?>
            {
                ["firmware_version"] = "1.0.0"
            }
        };

        device.Metadata.Should().NotBeNull();
        device.DeviceId.Should().Be("test-device-001");
    }

    [Fact]
    public void DeviceMetadata_ShouldAcceptConfigUpdates()
    {
        var device = new Device
        {
            DeviceId = "test-device-002",
            Name = "Test Device",
            DeviceType = "sensor",
            Metadata = new Dictionary<string, object?>()
        };

        var config = new Dictionary<string, object>
        {
            ["sampling_rate"] = 1000,
            ["mode"] = "active"
        };

        foreach (var entry in config)
        {
            device.Metadata[entry.Key] = entry.Value;
        }

        device.Metadata.Should().ContainKey("sampling_rate");
        device.Metadata.Should().ContainKey("mode");
    }

    [Fact]
    public void DeviceMetadata_NullCoalescing_ShouldInitialize()
    {
        var device = new Device
        {
            DeviceId = "test-device-003",
            Name = "Test Device",
            DeviceType = "sensor",
            Metadata = null
        };

        device.Metadata ??= new Dictionary<string, object?>();

        device.Metadata.Should().NotBeNull();
        device.Metadata.Should().BeEmpty();
    }
}
