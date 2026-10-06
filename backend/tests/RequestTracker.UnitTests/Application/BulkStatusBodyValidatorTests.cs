using RequestTracker.Application.Requests;

namespace RequestTracker.UnitTests.Application;

public class BulkStatusBodyValidatorTests
{
    private const string ValidVersion = "AAAAAAAAB9E=";
    private static readonly BulkStatusBodyValidator Validator = new();

    private static BulkStatusBody Body(int count = 1, string? status = "Completed") =>
        new(status, Enumerable.Range(1, count).Select(i => new BulkStatusItem(i, ValidVersion)).ToList());

    [Fact]
    public void A_valid_body_passes() => Assert.True(Validator.Validate(Body()).IsValid);

    [Theory]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(0, false)]
    [InlineData(101, false)]
    public void Item_count_must_be_between_1_and_100(int count, bool valid) =>
        Assert.Equal(valid, Validator.Validate(Body(count)).IsValid);

    [Fact]
    public void Missing_items_are_rejected() =>
        Assert.False(Validator.Validate(new BulkStatusBody("Completed", null)).IsValid);

    [Fact]
    public void Duplicate_ids_are_rejected()
    {
        var body = new BulkStatusBody("Completed", [new(5, ValidVersion), new(6, ValidVersion), new(5, ValidVersion)]);

        var result = Validator.Validate(body);

        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("duplicate"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64")]
    [InlineData("AAAA")] // base64, but not 8 bytes
    public void Versions_must_be_base64_of_8_bytes(string? version)
    {
        var body = new BulkStatusBody("Completed", [new(1, version)]);

        Assert.False(Validator.Validate(body).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Ids_must_be_positive(int id) =>
        Assert.False(Validator.Validate(new BulkStatusBody("Completed", [new(id, ValidVersion)])).IsValid);

    [Theory]
    [InlineData("Completed", true)]
    [InlineData("inprogress", true)]
    [InlineData("Done", false)]
    [InlineData("1", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Target_status_must_be_a_known_name(string? status, bool valid) =>
        Assert.Equal(valid, Validator.Validate(Body(1, status)).IsValid);
}
