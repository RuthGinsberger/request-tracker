using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RequestTracker.Api.Errors;

namespace RequestTracker.UnitTests.Api;

public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task Unexpected_exception_writes_500_problem_without_internals_and_logs_one_error()
    {
        var logger = new ListLogger();
        var (handler, context, body) = Create(Environments.Production, logger);

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("secret connection detail"), default);

        Assert.True(handled);
        Assert.Equal(500, context.Response.StatusCode);
        var json = body();
        Assert.Contains("internal-error", json);
        Assert.Contains("traceId", json);
        Assert.DoesNotContain("secret connection detail", json);
        Assert.DoesNotContain("stackTrace", json);
        Assert.Equal([LogLevel.Error], logger.Levels);
    }

    [Fact]
    public async Task Development_adds_exception_details()
    {
        var (handler, context, body) = Create(Environments.Development, new ListLogger());

        await handler.TryHandleAsync(context, new InvalidOperationException("boom"), default);

        Assert.Contains("boom", body());
        Assert.Contains("stackTrace", body());
    }

    [Fact]
    public async Task Client_abort_is_not_an_error_and_writes_no_body()
    {
        var logger = new ListLogger();
        var (handler, context, body) = Create(Environments.Production, logger);
        using var aborted = new CancellationTokenSource();
        context.RequestAborted = aborted.Token;
        await aborted.CancelAsync();

        var handled = await handler.TryHandleAsync(context, new OperationCanceledException(aborted.Token), default);

        Assert.True(handled);
        Assert.Equal(GlobalExceptionHandler.ClientClosedRequestStatusCode, context.Response.StatusCode);
        Assert.Empty(body());
        Assert.DoesNotContain(LogLevel.Error, logger.Levels);
    }

    [Fact]
    public async Task Cancellation_not_caused_by_the_client_is_still_an_error()
    {
        var logger = new ListLogger();
        var (handler, context, _) = Create(Environments.Production, logger);

        await handler.TryHandleAsync(context, new OperationCanceledException(), default);

        Assert.Equal(500, context.Response.StatusCode);
        Assert.Contains(LogLevel.Error, logger.Levels);
    }

    private static (GlobalExceptionHandler Handler, DefaultHttpContext Context, Func<string> Body) Create(
        string environment, ListLogger logger)
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var stream = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = stream;
        var handler = new GlobalExceptionHandler(
            services.GetRequiredService<IProblemDetailsService>(), new FakeEnvironment(environment), logger);
        return (handler, context, () => System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Tests";

        public string ContentRootPath { get; set; } = string.Empty;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class ListLogger : ILogger<GlobalExceptionHandler>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Levels.Add(logLevel);
    }
}
