using RequestTracker.Application.Requests;
using RequestTracker.Domain;

namespace RequestTracker.UnitTests.Application;

public class ListQueryHelpersTests
{
    [Theory]
    [InlineData("abc", "abc%")]
    [InlineData("", "%")]
    [InlineData("100%", "100\\%%")]
    [InlineData("a_b", "a\\_b%")]
    [InlineData("[x]", "\\[x]%")]
    [InlineData("c:\\dir", "c:\\\\dir%")]
    [InlineData("%_[\\", "\\%\\_\\[\\\\%")]
    public void LikePattern_escapes_wildcards_and_appends_the_prefix_wildcard(string input, string expected) =>
        Assert.Equal(expected, LikePattern.StartsWith(input));

    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(1, 20, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    [InlineData(100_000, 20, 5000)]
    [InlineData(100_001, 100, 1001)]
    [InlineData(int.MaxValue, 100, 21_474_837)]
    public void PagedResult_total_pages_is_ceiling_and_zero_when_empty(int totalCount, int pageSize, int expectedPages)
    {
        var result = PagedResult<int>.Create([], 1, pageSize, totalCount);

        Assert.Equal(expectedPages, result.TotalPages);
        Assert.Equal(totalCount, result.TotalCount);
    }

    [Theory]
    [InlineData("New", true, RequestStatus.New)]
    [InlineData("inprogress", true, RequestStatus.InProgress)]
    [InlineData("1", false, default(RequestStatus))]
    [InlineData("New,Waiting", false, default(RequestStatus))]
    [InlineData(null, false, default(RequestStatus))]
    public void TryParseEnum_accepts_only_names(string? value, bool ok, RequestStatus expected)
    {
        Assert.Equal(ok, QueryValues.TryParseEnum<RequestStatus>(value, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  x ", "x")]
    public void TrimToNull_treats_blank_as_not_provided(string? value, string? expected) =>
        Assert.Equal(expected, QueryValues.TrimToNull(value));

    [Fact]
    public void Sorting_defaults_to_createdAt_descending()
    {
        Assert.True(RequestSorting.TryParseField(null, out var field));
        Assert.True(RequestSorting.TryParseDirection(null, out var descending));

        Assert.Equal(RequestSortField.CreatedAt, field);
        Assert.True(descending);
    }

    [Fact]
    public void Sort_direction_parses_asc_and_desc_ignoring_case()
    {
        Assert.True(RequestSorting.TryParseDirection("ASC", out var asc));
        Assert.False(asc);
        Assert.True(RequestSorting.TryParseDirection("Desc", out var desc));
        Assert.True(desc);
    }

    [Fact]
    public void ToListItem_copies_every_field_and_encodes_the_version()
    {
        var createdAt = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var request = Request.Create("Title", "Org", RequestPriority.High, "dana", createdAt, RequestStatus.Waiting);

        var dto = RequestMappings.ToListItem.Compile()(request);

        Assert.Equal(
            new RequestListItemDto(0, "Title", "Org", RequestStatus.Waiting, RequestPriority.High, "dana", createdAt, createdAt, string.Empty),
            dto);
    }
}
