using FluentValidation;
using RequestTracker.Domain;

namespace RequestTracker.Application.Requests;

public sealed class RequestListQueryValidator : AbstractValidator<RequestListQuery>
{
    public RequestListQueryValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, RequestListQuery.MaxPage);
        RuleFor(q => q.PageSize).InclusiveBetween(1, RequestListQuery.MaxPageSize);

        RuleFor(q => q.Search)
            .Must(s => QueryValues.TrimToNull(s) is not { Length: > RequestListQuery.MaxSearchLength })
            .WithMessage($"'Search' must be at most {RequestListQuery.MaxSearchLength} characters.");

        RuleFor(q => q.Status)
            .Must(values => values is null || values.All(v => QueryValues.TryParseEnum<RequestStatus>(v, out _)))
            .WithMessage($"'Status' must be one of: {QueryValues.AllowedValues<RequestStatus>()}.");
        RuleFor(q => q.Priority)
            .Must(values => values is null || values.All(v => QueryValues.TryParseEnum<RequestPriority>(v, out _)))
            .WithMessage($"'Priority' must be one of: {QueryValues.AllowedValues<RequestPriority>()}.");

        RuleFor(q => q.OrganizationName).MaximumLength(Request.OrganizationNameMaxLength);
        RuleFor(q => q.AssignedTo).MaximumLength(Request.AssignedToMaxLength);

        RuleFor(q => q.CreatedTo)
            .Must((q, to) => q.CreatedFrom is not { } from || to is not { } end || from <= end)
            .WithMessage("'CreatedTo' must not be before 'CreatedFrom'.");

        RuleFor(q => q.SortBy)
            .Must(value => RequestSorting.TryParseField(value, out _))
            .WithMessage($"'SortBy' must be one of: {RequestSorting.AllowedFields}.");
        RuleFor(q => q.SortDirection)
            .Must(value => RequestSorting.TryParseDirection(value, out _))
            .WithMessage("'SortDirection' must be one of: asc, desc.");
    }
}
