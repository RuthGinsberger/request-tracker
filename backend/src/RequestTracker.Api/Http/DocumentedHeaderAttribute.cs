namespace RequestTracker.Api.Http;

/// <summary>
/// Declares a request header the action reads itself (so the framework cannot infer it) for the OpenAPI document.
/// Behaviour is unchanged: the header is still validated in the action.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class DocumentedHeaderAttribute(string name, string description) : Attribute
{
    public string Name { get; } = name;

    public string Description { get; } = description;
}
