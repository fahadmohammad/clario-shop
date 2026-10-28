using System.Security.Cryptography.X509Certificates;
using ClarioShop.Identity.Features.Tokens;
using ClarioShop.Identity.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ClarioShop.Identity.Services;

internal static class TokenServerRegistration
{
    public const string TokenEndpoint = "api/identity/token";

    /// <summary>
    /// OpenIddict token server (password, shopper OTP and refresh token grants) and local validation of its access tokens.
    /// Token entries live in the tenant's identity schema, so a refresh token only works on the shop that issued it.
    /// </summary>
    public static IServiceCollection AddTokenServer(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var auth = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        services.AddSingleton(Options.Create(auth));

        services.AddOpenIddict()
            .AddCore(core => core.UseEntityFrameworkCore().UseDbContext<IdentityDbContext>().ReplaceDefaultEntities<Guid>())
            .AddServer(server =>
            {
                server.SetIssuer(new Uri(auth.Issuer, UriKind.Absolute))
                    .SetTokenEndpointUris(TokenEndpoint)
                    .AllowPasswordFlow()
                    .AllowRefreshTokenFlow()
                    .AllowCustomFlow(OtpGrant.GrantType)
                    .AcceptAnonymousClients()
                    .RegisterScopes(Scopes.OfflineAccess)
                    .SetAccessTokenLifetime(auth.AccessTokenLifetime)
                    .SetRefreshTokenLifetime(auth.RefreshTokenLifetime)
                    .SetRefreshTokenReuseLeeway(auth.RefreshTokenReuseLeeway);

                AddKeys(server, auth, environment);

                var aspNetCore = server.UseAspNetCore().EnableTokenEndpointPassthrough();
                if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
                {
                    aspNetCore.DisableTransportSecurityRequirement();
                }
            })
            .AddValidation(validation =>
            {
                validation.UseLocalServer();
                validation.AddAudiences(auth.Audience);
                validation.UseAspNetCore();
            });

        services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        services.AddScoped<TokenPrincipalFactory>();
        services.AddScoped<OtpRequestThrottle>();

        // Redis when the host registered it; otherwise in-memory (single instance, development and tests).
        services.AddDistributedMemoryCache();
        return services;
    }

    private static void AddKeys(OpenIddictServerBuilder server, AuthOptions auth, IHostEnvironment environment)
    {
        if (!string.IsNullOrEmpty(auth.SigningCertificate) && !string.IsNullOrEmpty(auth.EncryptionCertificate))
        {
            server.AddSigningCertificate(LoadCertificate(auth.SigningCertificate, auth.CertificatePassword))
                .AddEncryptionCertificate(LoadCertificate(auth.EncryptionCertificate, auth.CertificatePassword));
        }
        else if (environment.IsDevelopment())
        {
            // Self-signed certificates kept in the user's certificate store, so tokens survive restarts.
            server.AddDevelopmentSigningCertificate().AddDevelopmentEncryptionCertificate();
        }
        else if (environment.IsEnvironment("Testing"))
        {
            server.AddEphemeralSigningKey().AddEphemeralEncryptionKey();
        }
        else
        {
            throw new InvalidOperationException("Auth:SigningCertificate and Auth:EncryptionCertificate must be configured.");
        }
    }

    private static X509Certificate2 LoadCertificate(string base64, string? password) =>
        X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(base64), password, X509KeyStorageFlags.EphemeralKeySet);
}
