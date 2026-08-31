using Xunit;
using NatureOS.CoreApi.Services;

namespace NatureOS.Tests;

/// <summary>
/// Tests for MYCA response model and synthetic flag (issue #5).
/// </summary>
public class MycaResponseTests
{
    [Fact]
    public void MycaResponse_DefaultSyntheticIsFalse()
    {
        var response = new MycaResponse();
        Assert.False(response.Synthetic);
    }

    [Fact]
    public void MycaResponse_CanMarkAsSynthetic()
    {
        var response = new MycaResponse { Synthetic = true };
        Assert.True(response.Synthetic);
    }

    [Fact]
    public void MycaResponse_LiveResponseIsNotSynthetic()
    {
        var response = new MycaResponse
        {
            Answer = "Live data answer",
            Confidence = 0.95,
            Timestamp = DateTime.UtcNow,
            Synthetic = false
        };

        Assert.False(response.Synthetic);
        Assert.NotEmpty(response.Answer);
        Assert.True(response.Confidence > 0);
    }
}
