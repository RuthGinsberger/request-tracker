using RequestTracker.Seeder;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

return await SeederApp.RunAsync(args, Console.Out, Console.Error, cts.Token);
