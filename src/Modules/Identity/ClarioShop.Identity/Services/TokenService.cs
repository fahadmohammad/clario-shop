using System.Security.Claims;
using System.Text;
using ClarioShop.Identity.Domain;
using ClarioShop.SharedKernel.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ClarioShop.Identity.Services;

/// <summary>Phase 0 token issuer (HMAC JWT). Replaced by OpenIddict in Phase 1.</summary>
internal sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    public AccessToken Create(User user, IEnumerable<string> permissions, Guid tenantId)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();
        var expires = now.Add(settings.AccessTokenLifetime);

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(ClarioClaimTypes.Tenant, tenantId.ToString()),
            .. permissions.Select(permission => new Claim(ClarioClaimTypes.Permission, permission)),
        ];

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}
