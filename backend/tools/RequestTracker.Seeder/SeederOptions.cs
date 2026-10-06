using System.Globalization;

namespace RequestTracker.Seeder;

public sealed record SeederOptions(int Count, int Seed, bool Reset, bool IfEmpty, DateTime AnchorUtc)
{
    public const int DefaultCount = 100_000;
    public const int DefaultSeed = 42;

    public const string Usage = """
        Usage: RequestTracker.Seeder [options]
          --count <n>         number of requests to generate (default 100000, minimum 1)
          --seed <n>          random seed; the same seed and anchor give identical data (default 42)
          --reset             drop and recreate the database via migrations, then seed
          --if-empty          seed only if Requests is empty; otherwise do nothing (used by Compose)
          --anchor <date>     yyyy-MM-dd (UTC) taken as "now" for CreatedAt; default today, set it to reproduce data across days
          --help              show this help
        Without --reset or --if-empty, a non-empty database is refused.
        The connection string comes from the ConnectionStrings__Default environment variable (default: LocalDB).
        """;

    public static SeederParseResult Parse(IReadOnlyList<string> args, TimeProvider timeProvider)
    {
        var count = DefaultCount;
        var seed = DefaultSeed;
        var reset = false;
        var ifEmpty = false;
        var anchor = timeProvider.GetUtcNow().UtcDateTime.Date;

        for (var i = 0; i < args.Count; i++)
        {
            var (name, inlineValue) = Split(args[i]);
            var optionIndex = i;

            // Value is either "--name=value" or the next argument.
            string? ReadValue()
            {
                if (inlineValue is not null)
                {
                    return inlineValue;
                }

                return i + 1 < args.Count ? args[++i] : null;
            }

            switch (name.ToLowerInvariant())
            {
                case "--help" or "-h" or "-?":
                    return SeederParseResult.Help;
                case "--reset":
                    reset = true;
                    break;
                case "--if-empty":
                    ifEmpty = true;
                    break;
                case "--count":
                    if (!int.TryParse(ReadValue(), NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count < 1)
                    {
                        return SeederParseResult.Fail("--count must be an integer of at least 1.");
                    }

                    break;
                case "--seed":
                    if (!int.TryParse(ReadValue(), NumberStyles.Integer, CultureInfo.InvariantCulture, out seed))
                    {
                        return SeederParseResult.Fail("--seed must be an integer.");
                    }

                    break;
                case "--anchor":
                    if (!DateTime.TryParseExact(
                            ReadValue(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    {
                        return SeederParseResult.Fail("--anchor must be a date in yyyy-MM-dd format.");
                    }

                    anchor = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
                    break;
                default:
                    return SeederParseResult.Fail($"Unknown option '{args[optionIndex]}'.");
            }
        }

        if (reset && ifEmpty)
        {
            return SeederParseResult.Fail("--reset and --if-empty cannot be combined.");
        }

        return SeederParseResult.Ok(new SeederOptions(count, seed, reset, ifEmpty, anchor));
    }

    private static (string Name, string? Value) Split(string arg)
    {
        var index = arg.IndexOf('=', StringComparison.Ordinal);
        return index < 0 ? (arg, null) : (arg[..index], arg[(index + 1)..]);
    }
}

public sealed record SeederParseResult(SeederOptions? Options, string? Error, bool ShowHelp)
{
    public static SeederParseResult Help { get; } = new(null, null, true);

    public static SeederParseResult Ok(SeederOptions options) => new(options, null, false);

    public static SeederParseResult Fail(string error) => new(null, error, false);
}
