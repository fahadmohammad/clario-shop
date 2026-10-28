using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ClarioShop.Api.Setup;

/// <summary>Adds the Bearer scheme so Scalar can send tokens.</summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "access_token from POST /api/identity/token (OAuth 2.0, form-encoded)",
        };

        return Task.CompletedTask;
    }
}
