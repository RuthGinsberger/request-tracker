using RequestTracker.Api.Http;

namespace RequestTracker.UnitTests.Api;

public class ETagHeaderTests
{
    [Fact]
    public void Format_wraps_the_version_in_double_quotes() =>
        Assert.Equal("\"AAAAAAAAB9E=\"", ETagHeader.Format("AAAAAAAAB9E="));
}
