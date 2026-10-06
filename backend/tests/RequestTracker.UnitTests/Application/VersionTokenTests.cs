using RequestTracker.Application;

namespace RequestTracker.UnitTests.Application;

public class VersionTokenTests
{
    [Fact]
    public void Encode_then_decode_round_trips_the_rowversion()
    {
        byte[] rowVersion = [0, 0, 0, 0, 0, 0, 0x07, 0xD1];

        var token = VersionToken.Encode(rowVersion);

        Assert.Equal("AAAAAAAAB9E=", token);
        Assert.True(VersionToken.TryDecode(token, out var decoded));
        Assert.Equal(rowVersion, decoded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("AAAA")]                  // valid base64, wrong length
    [InlineData("AAAAAAAAB9EAAAAA")]      // 12 bytes
    [InlineData("\"AAAAAAAAB9E=\"")]      // quotes are not part of the token
    public void TryDecode_rejects_anything_but_base64_of_eight_bytes(string? token)
    {
        Assert.False(VersionToken.TryDecode(token, out var decoded));
        Assert.Empty(decoded);
    }
}
