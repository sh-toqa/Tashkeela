using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

namespace Tashkeela.API.OpenApi;

internal static class OpenApiSetup
{
    private const string BearerScheme = "Bearer";

    /// <summary>OpenAPI document with a JWT "Authorize" button; anonymous endpoints are marked as such.</summary>
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services) =>
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Tashkeela API",
                    Version = "v1",
                    Description = "Current version: Teams and Events. In Development, get a token from POST /api/v1/dev/token, "
                        + "then click Authorize and paste it. Teams you don't belong to return 404; members without the "
                        + "Manager role get 403 for manager actions.",
                };
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                };
                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, _) =>
            {
                if (!context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = [],
                    });
                }

                return Task.CompletedTask;
            });
        });
}
