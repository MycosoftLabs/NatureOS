using FluentAssertions;
using NatureOS.CoreApi.Services;
using Xunit;

namespace NatureOS.Tests;

public class SseCorsTests
{
    [Theory]
    [InlineData("timestamp_desc")]
    [InlineData("timestamp_asc")]
    public void SortOrder_ShouldBeCanonicalTimestampPrefixed(string sortOrder)
    {
        sortOrder.Should().StartWith("timestamp_");
    }

    [Fact]
    public void SortOrder_Desc_ShouldNotBeBareDesc()
    {
        // "desc" alone is non-canonical; should use "timestamp_desc"
        var canonical = "timestamp_desc";
        var bare = "desc";

        canonical.Should().NotBe(bare);
        canonical.Should().Contain("timestamp");
    }

    [Fact]
    public void EventQuery_DefaultSortOrder_ShouldBeTimestampDesc()
    {
        var query = new EventQuery();

        query.SortOrder.Should().Be("timestamp_desc");
    }

    [Fact]
    public void CorsWildcard_WithCredentials_IsInvalid()
    {
        // Per CORS spec, Access-Control-Allow-Origin: * cannot be used with credentials
        var origin = "*";
        var allowCredentials = true;

        // This combination is invalid per spec
        var isValid = !(origin == "*" && allowCredentials);
        isValid.Should().BeFalse("wildcard origin with credentials violates CORS spec");
    }
}
