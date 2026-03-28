using FluentAssertions;
using NatureOS.CoreApi.Services;
using Xunit;

namespace NatureOS.Tests;

public class MycaResponseTests
{
    [Fact]
    public void SyntheticResponse_ShouldHaveZeroConfidence()
    {
        var response = new MycaResponse
        {
            Answer = "[Synthetic] Test response",
            Confidence = 0.0,
            Synthetic = true,
            Timestamp = DateTime.UtcNow
        };

        response.Synthetic.Should().BeTrue();
        response.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void SyntheticResponse_ShouldHaveSyntheticPrefix()
    {
        var response = new MycaResponse
        {
            Answer = "[Synthetic] Device data is available via the /devices endpoint.",
            Synthetic = true,
            Confidence = 0.0,
            Timestamp = DateTime.UtcNow
        };

        response.Answer.Should().StartWith("[Synthetic]");
    }

    [Fact]
    public void LiveResponse_ShouldNotBeSynthetic()
    {
        var response = new MycaResponse
        {
            Answer = "Your devices are operating normally.",
            Confidence = 0.92,
            Synthetic = false,
            Timestamp = DateTime.UtcNow
        };

        response.Synthetic.Should().BeFalse();
        response.Confidence.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DefaultResponse_ShouldNotBeSynthetic()
    {
        var response = new MycaResponse();

        response.Synthetic.Should().BeFalse();
        response.Confidence.Should().Be(0);
        response.Answer.Should().BeEmpty();
    }

    [Fact]
    public void SuggestedQuestions_CanBeNull()
    {
        var response = new MycaResponse
        {
            Answer = "Test",
            Timestamp = DateTime.UtcNow
        };

        response.SuggestedQuestions.Should().BeNull();
    }

    [Fact]
    public void SuggestedQuestions_CanHaveItems()
    {
        var response = new MycaResponse
        {
            Answer = "Test",
            Timestamp = DateTime.UtcNow,
            SuggestedQuestions = new[] { "Q1", "Q2", "Q3" }
        };

        response.SuggestedQuestions.Should().HaveCount(3);
    }
}
