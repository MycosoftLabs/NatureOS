using FluentAssertions;
using NatureOS.CoreApi.Services;
using Xunit;

namespace NatureOS.Tests;

public class MasIngestionResultTests
{
    [Fact]
    public void Ok_ShouldReturnSuccessResult()
    {
        var result = MasIngestionResult.Ok("doc-123");

        result.Success.Should().BeTrue();
        result.DocumentId.Should().Be("doc-123");
        result.Error.Should().BeNull();
        result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Fail_ShouldReturnErrorResult()
    {
        var result = MasIngestionResult.Fail("Something went wrong");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Something went wrong");
        result.DocumentId.Should().BeNull();
        result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Fail_NullPayload_ShouldReturnDescriptiveError()
    {
        var result = MasIngestionResult.Fail("Payload is null");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("null");
    }

    [Fact]
    public void Fail_MissingEventId_ShouldReturnDescriptiveError()
    {
        var result = MasIngestionResult.Fail("EventId is missing");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("EventId");
    }

    [Fact]
    public void Ok_Timestamp_ShouldBeUtcNow()
    {
        var before = DateTime.UtcNow;
        var result = MasIngestionResult.Ok("doc-456");
        var after = DateTime.UtcNow;

        result.Timestamp.Should().BeOnOrAfter(before);
        result.Timestamp.Should().BeOnOrBefore(after);
    }
}
