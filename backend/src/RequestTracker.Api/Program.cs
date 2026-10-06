using RequestTracker.Api.Startup;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApi();

var app = builder.Build();

app.LogStartupSummary();
app.UseApi();

app.Run();

// Exposed so integration tests can use WebApplicationFactory<Program>.
public partial class Program;
