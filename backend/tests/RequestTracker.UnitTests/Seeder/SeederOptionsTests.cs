using RequestTracker.Seeder;

namespace RequestTracker.UnitTests.Seeder;

public class SeederOptionsTests
{
    private static SeederParseResult Parse(params string[] args) => SeederOptions.Parse(args, TimeProvider.System);

    [Fact]
    public void Defaults_are_100k_with_fixed_seed()
    {
        var options = Parse().Options;

        Assert.NotNull(options);
        Assert.Equal(100_000, options.Count);
        Assert.Equal(SeederOptions.DefaultSeed, options.Seed);
        Assert.False(options.Reset);
        Assert.False(options.IfEmpty);
    }

    [Fact]
    public void Parses_all_options_in_both_value_forms()
    {
        var options = Parse("--count", "500", "--seed=9", "--reset", "--anchor", "2026-01-31").Options;

        Assert.NotNull(options);
        Assert.Equal(500, options.Count);
        Assert.Equal(9, options.Seed);
        Assert.True(options.Reset);
        Assert.Equal(new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc), options.AnchorUtc);
    }

    [Theory]
    [InlineData("--count", "0")]
    [InlineData("--count", "-5")]
    [InlineData("--count", "abc")]
    [InlineData("--count")]
    [InlineData("--seed", "x")]
    [InlineData("--anchor", "31/01/2026")]
    [InlineData("--bogus")]
    public void Invalid_input_is_a_usage_error(params string[] args)
    {
        var result = Parse(args);

        Assert.Null(result.Options);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public void Reset_and_if_empty_cannot_be_combined()
    {
        Assert.NotNull(Parse("--reset", "--if-empty").Error);
    }

    [Fact]
    public void Help_is_recognised()
    {
        Assert.True(Parse("--help").ShowHelp);
    }
}
