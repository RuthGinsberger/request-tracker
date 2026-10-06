namespace RequestTracker.Application.Requests;

public sealed record LookupsDto(IReadOnlyList<string> Organizations, IReadOnlyList<string> Assignees);
