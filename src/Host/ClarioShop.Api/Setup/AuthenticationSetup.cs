using System.Security.Cryptography;
using System.Text;
using ClarioShop.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClarioShop.Api.Setup;

internal static class AuthenticationSetup
{
    public static IServiceCollection AddClarioAuthentication(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .PostConfigure(options =>
            {
                // Development without a configured key: a random key per run (tokens die on restart). Never in production.
                if (string.IsNullOrEmpty(options.SigningKey) && environment.IsDevelopment())
                {
                    options.SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
                }
            })
            .Validate(options => Encoding.UTF8.GetByteCount(options.SigningKey) >= 32, "Auth:SigningKey must be at least 32 bytes.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                var settings = jwt.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = settings.Issuer,
                    ValidAudience = settings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                };
            });

        return services;
    }
}
