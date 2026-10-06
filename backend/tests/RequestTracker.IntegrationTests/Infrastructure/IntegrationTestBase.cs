namespace RequestTracker.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "Database";
}

/// <summary>Base class: tests in the collection run one at a time, each starting from an empty database.</summary>
[Collection(DatabaseCollection.Name)]
public abstract class IntegrationTestBase(SqlServerFixture fixture) : IAsyncLifetime
{
    private HttpClient? client;

    protected SqlServerFixture Fixture { get; } = fixture;

    protected HttpClient Client => client ??= Fixture.Factory.CreateClient();

    protected TestData Data => new(Fixture);

    public async Task InitializeAsync() => await Fixture.ResetAsync();

    public Task DisposeAsync()
    {
        client?.Dispose();
        return Task.CompletedTask;
    }
}
