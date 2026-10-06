using FluentValidation;
using RequestTracker.Domain;

namespace RequestTracker.Application.Requests;

/// <summary>Body of <c>PATCH /api/requests/{id}/status</c>. A string so a bad value gives one clear error (D-039).</summary>
public sealed record ChangeStatusBody(string? Status);

public sealed class ChangeStatusBodyValidator : AbstractValidator<ChangeStatusBody>
{
    public ChangeStatusBodyValidator() =>
        RuleFor(b => b.Status)
            .Must(value => QueryValues.TryParseEnum<RequestStatus>(value, out _))
            .WithMessage($"Status is required and must be one of: {QueryValues.AllowedValues<RequestStatus>()}.");
}
