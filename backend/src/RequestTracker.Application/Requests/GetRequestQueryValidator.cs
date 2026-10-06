using FluentValidation;

namespace RequestTracker.Application.Requests;

public sealed class GetRequestQueryValidator : AbstractValidator<GetRequestQuery>
{
    public GetRequestQueryValidator() => RuleFor(q => q.Id).GreaterThanOrEqualTo(1);
}
