using Xunit;
using NatureOS.CoreApi.Services;

namespace NatureOS.Tests;

/// <summary>
/// Tests for the MAS health result model (issue #8).
/// Validates that the read-only health check result correctly
/// reports healthy/unhealthy states.
/// </summary>
public class MasHealthResultTests
{
    [Fact]
    public void MasHealthResult_Healthy()
    {
        var result = new MasHealthResult
        {
            Healthy = true,
            Status = "Healthy",
            Detail = "mas_context reachable, 42 document(s)"
        };

        Assert.True(result.Healthy);
        Assert.Equal("Healthy", result.Status);
        Assert.Contains("42", result.Detail);
    }

    [Fact]
    public void MasHealthResult_Unhealthy()
    {
        var result = new MasHealthResult
        {
            Healthy = false,
            Status = "Unhealthy",
            Detail = "Connection refused"
        };

        Assert.False(result.Healthy);
        Assert.Equal("Unhealthy", result.Status);
    }

    [Fact]
    public void MasHealthResult_DefaultStatus()
    {
        var result = new MasHealthResult { Healthy = false };
        Assert.Equal("Unknown", result.Status);
    }
}
