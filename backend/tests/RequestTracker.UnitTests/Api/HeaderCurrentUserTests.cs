using Microsoft.AspNetCore.Http;
using RequestTracker.Api.Http;

namespace RequestTracker.UnitTests.Api;

public class HeaderCurrentUserTests
{
    [Theory]
    [InlineData("dana", "dana")]
    [InlineData("  dana ", "dana")]
    public void Valid_header_gives_the_trimmed_name(string header, string expected) =>
        Assert.Equal(expected, UserFor(header).UserName);

    [Fact]
    public void Missing_header_gives_null() => Assert.Null(UserFor(null).UserName);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_header_gives_null(string header) => Assert.Null(UserFor(header).UserName);

    [Fact]
    public void Too_long_header_gives_null() => Assert.Null(UserFor(new string('x', 101)).UserName);

    [Fact]
    public void Repeated_header_gives_null()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderCurrentUser.HeaderName] = new[] { "dana", "noa" };

        Assert.Null(new HeaderCurrentUser(new HttpContextAccessor { HttpContext = context }).UserName);
    }

    private static HeaderCurrentUser UserFor(string? header)
    {
        var context = new DefaultHttpContext();
        if (header is not null)
        {
            context.Request.Headers[HeaderCurrentUser.HeaderName] = header;
        }

        return new HeaderCurrentUser(new HttpContextAccessor { HttpContext = context });
    }
}
