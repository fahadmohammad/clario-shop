namespace ClarioShop.Identity.Services;

internal sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
