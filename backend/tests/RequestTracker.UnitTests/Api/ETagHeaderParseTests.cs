using RequestTracker.Api.Http;

namespace RequestTracker.UnitTests.Api;

public sealed class ETagHeaderParseTests
{
    [Fact]
    public void Quoted_base64_of_eight_bytes_is_accepted()
    {
        Assert.True(ETagHeader.TryParseVersion("\"AAAAAAAAB9E=\"", out var version));
        Assert.Equal([0, 0, 0, 0, 0, 0, 0x07, 0xD1], version);
    }

    [Fact]
    public void Round_trips_with_format()
    {
        var token = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]);
        Assert.True(ETagHeader.TryParseVersion(ETagHeader.Format(token), out var version));
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], version);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("W/\"AAAAAAAAB9E=\"")]
    [InlineData("AAAAAAAAB9E=")]
    [InlineData("\"AAAAAAAAB9E=")]
    [InlineData("\"\"")]
    [InlineData("\"AAAA\"")]
    [InlineData("\"%%%%%%%%%%%=\"")]
    [InlineData("\"AAAAAAAAB9E=\", \"AAAAAAAAB9F=\"")]
    [InlineData("")]
    public void Anything_else_is_rejected(string header) =>
        Assert.False(ETagHeader.TryParseVersion(header, out _));

    [Fact]
    public void Several_header_lines_are_rejected() =>
        Assert.False(ETagHeader.TryParseVersion(new Microsoft.Extensions.Primitives.StringValues(["\"AAAAAAAAB9E=\"", "\"AAAAAAAAB9F=\""]), out _));
}
