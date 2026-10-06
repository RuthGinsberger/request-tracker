using System.Globalization;
using RequestTracker.Domain;

namespace RequestTracker.Seeder;

/// <summary>
/// Produces a deterministic, realistic stream of requests: the same seed and anchor always give the same rows.
/// Distributions: Status 15% New / 25% InProgress / 15% Waiting / 45% Completed; Priority 40% Low / 40% Medium / 20% High;
/// 300 organizations (skewed so a few are large customers) and 40 handlers; 30% of New requests are unassigned;
/// CreatedAt uniform over the 24 months before the anchor; UpdatedAt = CreatedAt for New, later (up to the anchor) otherwise.
/// </summary>
public sealed class RequestGenerator
{
    public const int HistoryMonths = 24;

    private const int UnassignedNewPercent = 30;

    private static readonly string[] OrganizationPrefixes =
    [
        "Acme", "Orion", "Galil", "Carmel", "Negev", "Sharon", "Tavor", "Hermon", "Arava", "Kinneret",
        "Zohar", "Maple", "Cedar", "Olive", "Pioneer", "Summit", "Horizon", "Atlas", "Nimbus", "Vertex",
        "Lotus", "Falcon", "Granite", "Harbor", "Meridian", "Nova", "Quantum", "Beacon", "Delta", "Evergreen",
    ];

    private static readonly string[] OrganizationSuffixes =
    [
        "Ltd", "Systems", "Logistics", "Foods", "Energy", "Health", "Construction", "Consulting", "Technologies", "Municipality",
    ];

    private static readonly string[] HandlerFirstNames = ["dana", "yossi", "noa", "avi", "michal", "ron", "tamar", "eli"];

    private static readonly string[] HandlerLastNames = ["cohen", "levi", "mizrahi", "peretz", "katz"];

    private static readonly string[] Systems =
    [
        "payroll", "CRM", "VPN", "email", "ERP", "intranet", "billing portal", "document archive",
        "HR system", "timesheet", "procurement", "reporting dashboard", "badge access", "file share", "ticketing",
    ];

    // {0} = system, {1} = reference number, {2} = year. Different leading words keep prefix search meaningful.
    private static readonly string[] TitleTemplates =
    [
        "Access request for {0}",
        "Billing discrepancy on invoice #{1}",
        "Password reset for {0} account",
        "License renewal for {0}",
        "Permit application #{1}",
        "Complaint regarding delayed shipment #{1}",
        "Onboarding of new employee to {0}",
        "Data export from {0}",
        "Annual compliance report {2}",
        "Incident report: {0} outage",
        "Refund request for order #{1}",
        "Update contact details on {0}",
        "Contract amendment #{1}",
        "Question about {0} permissions",
    ];

    private static readonly (RequestStatus Value, int Weight)[] StatusWeights =
    [
        (RequestStatus.New, 15), (RequestStatus.InProgress, 25), (RequestStatus.Waiting, 15), (RequestStatus.Completed, 45),
    ];

    private static readonly (RequestPriority Value, int Weight)[] PriorityWeights =
    [
        (RequestPriority.Low, 40), (RequestPriority.Medium, 40), (RequestPriority.High, 20),
    ];

    private readonly int _seed;
    private readonly DateTime _anchorUtc;

    public RequestGenerator(int seed, DateTime anchorUtc)
    {
        if (anchorUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The anchor must be UTC.", nameof(anchorUtc));
        }

        _seed = seed;
        _anchorUtc = anchorUtc;
    }

    public static IReadOnlyList<string> Organizations { get; } =
        [.. OrganizationPrefixes.SelectMany(p => OrganizationSuffixes.Select(s => $"{p} {s}"))];

    public static IReadOnlyList<string> Handlers { get; } =
        [.. HandlerFirstNames.SelectMany(f => HandlerLastNames.Select(l => $"{f}.{l}"))];

    /// <summary>Lazily yields <paramref name="count"/> requests; nothing is buffered.</summary>
    public IEnumerable<Request> Generate(int count)
    {
        var random = new Random(_seed);
        var windowStart = _anchorUtc.AddMonths(-HistoryMonths);
        var windowSeconds = (long)(_anchorUtc - windowStart).TotalSeconds;

        for (var i = 0; i < count; i++)
        {
            var status = Pick(random, StatusWeights);
            var priority = Pick(random, PriorityWeights);

            var createdAt = windowStart.AddSeconds(random.NextInt64(windowSeconds));
            var updatedAt = createdAt;
            if (status != RequestStatus.New)
            {
                var remainingSeconds = (long)(_anchorUtc - createdAt).TotalSeconds;
                updatedAt = createdAt.AddSeconds(random.NextInt64(1, remainingSeconds + 1));
            }

            var unassigned = status == RequestStatus.New && random.Next(100) < UnassignedNewPercent;
            var assignedTo = unassigned ? null : Handlers[random.Next(Handlers.Count)];

            // Squaring a uniform value skews towards the first organizations (about 6% of rows for the largest).
            var organizationRoll = random.NextDouble();
            var organization = Organizations[(int)(Organizations.Count * organizationRoll * organizationRoll)];

            yield return Request.Create(
                BuildTitle(random, createdAt.Year),
                organization,
                priority,
                assignedTo,
                createdAt,
                status,
                updatedAt);
        }
    }

    private static string BuildTitle(Random random, int year)
    {
        var template = TitleTemplates[random.Next(TitleTemplates.Length)];
        var system = Systems[random.Next(Systems.Length)];
        var reference = random.Next(1000, 100_000);
        return string.Format(CultureInfo.InvariantCulture, template, system, reference, year);
    }

    private static T Pick<T>(Random random, (T Value, int Weight)[] table)
    {
        var roll = random.Next(table.Sum(entry => entry.Weight));
        foreach (var (value, weight) in table)
        {
            if (roll < weight)
            {
                return value;
            }

            roll -= weight;
        }

        throw new InvalidOperationException("Unreachable: the roll is below the total weight.");
    }
}
