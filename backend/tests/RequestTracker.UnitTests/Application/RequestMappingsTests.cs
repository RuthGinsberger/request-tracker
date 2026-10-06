using RequestTracker.Application.Requests;
using RequestTracker.Domain;

namespace RequestTracker.UnitTests.Application;

public class RequestMappingsTests
{
    [Fact]
    public void ToDetails_copies_every_field_and_encodes_the_version()
    {
        var createdAt = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var request = Request.Create("Title", "Org", RequestPriority.Low, null, createdAt);

        var dto = RequestMappings.ToDetails.Compile()(request);

        Assert.Equal(new RequestDetailsDto(
            0, "Title", "Org", RequestStatus.New, RequestPriority.Low, null, createdAt, createdAt, string.Empty), dto);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    [InlineData(int.MaxValue, true)]
    public void GetRequestQuery_requires_a_positive_id(int id, bool valid) =>
        Assert.Equal(valid, new GetRequestQueryValidator().Validate(new GetRequestQuery(id)).IsValid);
}
