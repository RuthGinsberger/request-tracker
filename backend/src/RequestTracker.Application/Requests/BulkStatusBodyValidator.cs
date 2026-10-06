using FluentValidation;
using RequestTracker.Domain;

namespace RequestTracker.Application.Requests;

/// <summary>Whole-request validation: any failure means nothing is processed (400).</summary>
public sealed class BulkStatusBodyValidator : AbstractValidator<BulkStatusBody>
{
    public const int MaxItems = 100;

    public BulkStatusBodyValidator()
    {
        RuleFor(b => b.TargetStatus)
            .Must(value => QueryValues.TryParseEnum<RequestStatus>(value, out _))
            .WithMessage($"TargetStatus is required and must be one of: {QueryValues.AllowedValues<RequestStatus>()}.");

        RuleFor(b => b.Items)
            .Must(items => items is { Count: >= 1 and <= MaxItems })
            .WithMessage($"Items must contain between 1 and {MaxItems} entries.");

        RuleForEach(b => b.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Id).GreaterThanOrEqualTo(1);
            item.RuleFor(i => i.Version)
                .Must(version => VersionToken.TryDecode(version, out _))
                .WithMessage("Version must be the base64 version returned by the API.");
        });

        RuleFor(b => b.Items)
            .Must(items => items!.Select(i => i.Id).Distinct().Count() == items!.Count)
            .When(b => b.Items is { Count: > 0 })
            .WithMessage("Items must not contain duplicate ids.");
    }
}
