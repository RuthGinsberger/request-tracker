using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using RequestTracker.Api.Http;

namespace RequestTracker.Api.Startup;

/// <summary>Adds the headers named by <see cref="DocumentedHeaderAttribute"/> to the operation as required header parameters.</summary>
public sealed class DocumentedHeaderTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var headers = context.Description.ActionDescriptor.EndpointMetadata.OfType<DocumentedHeaderAttribute>();
        foreach (var header in headers)
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = header.Name,
                In = ParameterLocation.Header,
                Required = true,
                Description = header.Description,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            });
        }

        return Task.CompletedTask;
    }
}
