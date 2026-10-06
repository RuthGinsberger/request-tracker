using RequestTracker.Domain;
using RequestTracker.Seeder;

namespace RequestTracker.UnitTests.Seeder;

public class RequestGeneratorTests
{
    private static readonly DateTime Anchor = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private static List<Request> Generate(int seed, int count = 20_000) =>
        [.. new RequestGenerator(seed, Anchor).Generate(count)];

    [Fact]
    public void Same_seed_produces_identical_data()
    {
        var first = Generate(7, 2_000);
        var second = Generate(7, 2_000);

        Assert.Equal(first.Select(Describe), second.Select(Describe));
    }

    [Fact]
    public void Different_seeds_produce_different_data()
    {
        Assert.NotEqual(Generate(1, 500).Select(Describe), Generate(2, 500).Select(Describe));
    }

    [Fact]
    public void Generates_exactly_the_requested_count()
    {
        Assert.Equal(1234, Generate(1, 1234).Count);
    }

    [Fact]
    public void Rows_respect_invariants_and_length_limits()
    {
        var windowStart = Anchor.AddMonths(-RequestGenerator.HistoryMonths);

        foreach (var r in Generate(3))
        {
            Assert.True(r.UpdatedAt >= r.CreatedAt);
            Assert.True(r.CreatedAt >= windowStart && r.CreatedAt < Anchor);
            Assert.True(r.UpdatedAt <= Anchor);
            Assert.Equal(DateTimeKind.Utc, r.CreatedAt.Kind);
            Assert.InRange(r.Title.Length, 1, Request.TitleMaxLength);
            Assert.InRange(r.OrganizationName.Length, 1, Request.OrganizationNameMaxLength);
            Assert.True(r.AssignedTo is null || r.AssignedTo.Length <= Request.AssignedToMaxLength);

            if (r.Status == RequestStatus.New)
            {
                Assert.Equal(r.CreatedAt, r.UpdatedAt);
            }
            else
            {
                Assert.NotNull(r.AssignedTo);
            }
        }
    }

    [Fact]
    public void Status_and_priority_follow_the_target_distribution()
    {
        var rows = Generate(11);

        AssertShare(rows, r => r.Status == RequestStatus.New, 0.15);
        AssertShare(rows, r => r.Status == RequestStatus.InProgress, 0.25);
        AssertShare(rows, r => r.Status == RequestStatus.Waiting, 0.15);
        AssertShare(rows, r => r.Status == RequestStatus.Completed, 0.45);
        AssertShare(rows, r => r.Priority == RequestPriority.Low, 0.40);
        AssertShare(rows, r => r.Priority == RequestPriority.Medium, 0.40);
        AssertShare(rows, r => r.Priority == RequestPriority.High, 0.20);
    }

    [Fact]
    public void Only_some_new_requests_are_unassigned()
    {
        var newRows = Generate(5).Where(r => r.Status == RequestStatus.New).ToList();

        AssertShare(newRows, r => r.AssignedTo is null, 0.30);
    }

    [Fact]
    public void Uses_about_300_organizations_and_40_handlers()
    {
        Assert.Equal(300, RequestGenerator.Organizations.Distinct().Count());
        Assert.Equal(40, RequestGenerator.Handlers.Distinct().Count());
        Assert.Contains("dana.cohen", RequestGenerator.Handlers);

        var rows = Generate(9);
        Assert.True(rows.Select(r => r.OrganizationName).Distinct().Count() > 250);
        Assert.Equal(40, rows.Select(r => r.AssignedTo).Where(a => a is not null).Distinct().Count());
    }

    [Fact]
    public void Titles_vary_enough_for_prefix_search()
    {
        var prefixes = Generate(4, 2_000).Select(r => r.Title.Split(' ')[0]).Distinct().Count();

        Assert.True(prefixes >= 8, $"Only {prefixes} distinct first words.");
    }

    [Fact]
    public void Non_utc_anchor_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new RequestGenerator(1, new DateTime(2026, 6, 1)));
    }

    private static void AssertShare(IReadOnlyCollection<Request> rows, Func<Request, bool> predicate, double expected)
    {
        var share = (double)rows.Count(predicate) / rows.Count;
        Assert.InRange(share, expected - 0.02, expected + 0.02);
    }

    private static string Describe(Request r) =>
        $"{r.Title}|{r.OrganizationName}|{r.Status}|{r.Priority}|{r.AssignedTo}|{r.CreatedAt:O}|{r.UpdatedAt:O}";
}
