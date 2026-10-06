using RequestTracker.Application;

namespace RequestTracker.UnitTests.Application;

public class UserNameRulesTests
{
    [Theory]
    [InlineData("dana", "dana")]
    [InlineData("  dana  ", "dana")]
    public void Valid_names_are_trimmed(string raw, string expected)
    {
        Assert.True(UserNameRules.TryNormalize(raw, out var name));
        Assert.Equal(expected, name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_names_are_rejected(string? raw) => Assert.False(UserNameRules.TryNormalize(raw, out _));

    [Fact]
    public void Length_limit_is_100_after_trimming()
    {
        Assert.True(UserNameRules.TryNormalize(new string('a', 100), out _));
        Assert.True(UserNameRules.TryNormalize(" " + new string('a', 100) + " ", out _));
        Assert.False(UserNameRules.TryNormalize(new string('a', 101), out _));
    }
}
