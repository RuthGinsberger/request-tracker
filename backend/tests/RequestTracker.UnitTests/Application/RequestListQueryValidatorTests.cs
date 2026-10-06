using FluentValidation.Results;
using RequestTracker.Application.Requests;

namespace RequestTracker.UnitTests.Application;

public class RequestListQueryValidatorTests
{
    private static readonly RequestListQueryValidator Validator = new();

    private static ValidationResult Validate(RequestListQuery query) => Validator.Validate(query);

    private static void AssertInvalid(RequestListQuery query, string property)
    {
        var result = Validate(query);
        Assert.Contains(result.Errors, e => e.PropertyName == property);
    }

    [Fact]
    public void Defaults_are_valid()
    {
        var query = new RequestListQuery();

        Assert.True(Validate(query).IsValid);
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(1_000_000, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1_000_001, false)]
    public void Page_must_be_between_1_and_1_000_000(int page, bool valid) =>
        Assert.Equal(valid, Validate(new RequestListQuery { Page = page }).IsValid);

    [Theory]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(0, false)]
    [InlineData(101, false)]
    [InlineData(-1, false)]
    public void PageSize_must_be_between_1_and_100(int pageSize, bool valid) =>
        Assert.Equal(valid, Validate(new RequestListQuery { PageSize = pageSize }).IsValid);

    [Fact]
    public void Search_limit_is_100_characters_after_trimming()
    {
        Assert.True(Validate(new RequestListQuery { Search = new string('a', 100) }).IsValid);
        Assert.True(Validate(new RequestListQuery { Search = "  " + new string('a', 100) + "  " }).IsValid);
        Assert.True(Validate(new RequestListQuery { Search = "   " }).IsValid);
        Assert.True(Validate(new RequestListQuery { Search = null }).IsValid);
        AssertInvalid(new RequestListQuery { Search = new string('a', 101) }, nameof(RequestListQuery.Search));
    }

    [Theory]
    [InlineData("New")]
    [InlineData("inprogress")]
    [InlineData("WAITING")]
    [InlineData("Completed")]
    public void Status_names_are_case_insensitive(string status) =>
        Assert.True(Validate(new RequestListQuery { Status = [status] }).IsValid);

    [Theory]
    [InlineData("Bogus")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData("New ")]
    public void Unknown_status_is_rejected_even_next_to_valid_ones(string bad) =>
        AssertInvalid(new RequestListQuery { Status = ["New", bad] }, nameof(RequestListQuery.Status));

    [Fact]
    public void Priority_values_are_checked_like_status()
    {
        Assert.True(Validate(new RequestListQuery { Priority = ["low", "High"] }).IsValid);
        AssertInvalid(new RequestListQuery { Priority = ["Urgent"] }, nameof(RequestListQuery.Priority));
        AssertInvalid(new RequestListQuery { Priority = ["2"] }, nameof(RequestListQuery.Priority));
    }

    [Fact]
    public void Organization_and_assignee_have_maximum_lengths()
    {
        Assert.True(Validate(new RequestListQuery
        {
            OrganizationName = new string('o', 200),
            AssignedTo = new string('n', 100),
        }).IsValid);
        AssertInvalid(new RequestListQuery { OrganizationName = new string('o', 201) }, nameof(RequestListQuery.OrganizationName));
        AssertInvalid(new RequestListQuery { AssignedTo = new string('n', 101) }, nameof(RequestListQuery.AssignedTo));
    }

    [Fact]
    public void CreatedFrom_must_not_be_after_CreatedTo()
    {
        var day = new DateOnly(2026, 1, 31);

        Assert.True(Validate(new RequestListQuery { CreatedFrom = day, CreatedTo = day }).IsValid);
        Assert.True(Validate(new RequestListQuery { CreatedFrom = day }).IsValid);
        Assert.True(Validate(new RequestListQuery { CreatedTo = day }).IsValid);
        AssertInvalid(
            new RequestListQuery { CreatedFrom = day.AddDays(1), CreatedTo = day }, nameof(RequestListQuery.CreatedTo));
    }

    [Theory]
    [InlineData("createdAt")]
    [InlineData("CREATEDAT")]
    [InlineData("updatedAt")]
    [InlineData("priority")]
    [InlineData("status")]
    [InlineData("title")]
    public void Whitelisted_sort_fields_are_accepted_case_insensitively(string sortBy) =>
        Assert.True(Validate(new RequestListQuery { SortBy = sortBy }).IsValid);

    [Theory]
    [InlineData("id")]
    [InlineData("organizationName")]
    [InlineData("0")]
    [InlineData("title; DROP TABLE Requests")]
    [InlineData("")]
    public void Other_sort_fields_are_rejected(string sortBy) =>
        AssertInvalid(new RequestListQuery { SortBy = sortBy }, nameof(RequestListQuery.SortBy));

    [Theory]
    [InlineData("asc", true)]
    [InlineData("DESC", true)]
    [InlineData("up", false)]
    [InlineData("", false)]
    public void Sort_direction_is_asc_or_desc(string direction, bool valid) =>
        Assert.Equal(valid, Validate(new RequestListQuery { SortDirection = direction }).IsValid);

    [Fact]
    public void Every_invalid_parameter_is_reported()
    {
        var result = Validate(new RequestListQuery { Page = 0, PageSize = 0, SortBy = "x", SortDirection = "x" });

        Assert.Equal(
            ["Page", "PageSize", "SortBy", "SortDirection"],
            result.Errors.Select(e => e.PropertyName).Order(StringComparer.Ordinal));
    }
}
