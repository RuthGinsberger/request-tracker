using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RequestTracker.Api.Errors;

/// <summary>
/// Runs the registered FluentValidation validator for every action argument and answers failures with the same
/// ValidationProblemDetails that model-binding errors use. Arguments without a validator are skipped.
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new Dictionary<string, List<string>>();
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null || context.HttpContext.RequestServices.GetService(
                    typeof(IValidator<>).MakeGenericType(argument.GetType())) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            foreach (var failure in result.Errors)
            {
                var key = ApiProblems.CamelCaseKey(failure.PropertyName);
                if (!errors.TryGetValue(key, out var messages))
                {
                    errors[key] = messages = [];
                }

                messages.Add(failure.ErrorMessage);
            }
        }

        if (errors.Count > 0)
        {
            var problem = ApiProblems.Validation(
                context.HttpContext, errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
            context.Result = new ProblemResult(problem);
            return;
        }

        await next();
    }
}
