using System.Globalization;
using ClarioShop.SharedKernel.Tenancy;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace ClarioShop.Identity.Services;

/// <summary>
/// Limits how many sign-in codes a shop sends per phone and per IP address, in fixed windows counted in the
/// distributed cache (Redis when configured, so every API instance shares the counts; in-memory otherwise).
/// Counting is read-then-write, so a burst of simultaneous requests can slip a few past the limit; the host's
/// per-address rate limiter on the endpoint bounds such bursts.
/// </summary>
internal sealed class OtpRequestThrottle(IDistributedCache cache, ITenantContext tenant, TimeProvider clock, IOptions<AuthOptions> options)
{
    /// <summary>Counts one request for the phone and the address; false when either is over its limit.</summary>
    public async Task<bool> TryAcquireAsync(string phone, string address, CancellationToken cancellationToken)
    {
        var otp = options.Value.Otp;
        var phoneAllowed = await CountAsync($"identity:otp:{tenant.TenantId:N}:phone:{phone}", otp.RequestsPerPhone, otp.RequestWindow, cancellationToken);
        var addressAllowed = await CountAsync($"identity:otp:{tenant.TenantId:N}:address:{address}", otp.RequestsPerAddress, otp.RequestWindow, cancellationToken);
        return phoneAllowed && addressAllowed;
    }

    private async Task<bool> CountAsync(string key, int limit, TimeSpan window, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var (count, windowEnd) = Parse(await cache.GetStringAsync(key, cancellationToken));
        if (windowEnd <= now)
        {
            (count, windowEnd) = (0, now + window);
        }

        count++;
        await cache.SetStringAsync(
            key,
            string.Create(CultureInfo.InvariantCulture, $"{count}|{windowEnd.ToUnixTimeMilliseconds()}"),
            new DistributedCacheEntryOptions { AbsoluteExpiration = windowEnd },
            cancellationToken);
        return count <= limit;
    }

    private static (int Count, DateTimeOffset WindowEnd) Parse(string? value)
    {
        var parts = value?.Split('|');
        return parts is [var count, var end]
            && int.TryParse(count, CultureInfo.InvariantCulture, out var parsedCount)
            && long.TryParse(end, CultureInfo.InvariantCulture, out var parsedEnd)
                ? (parsedCount, DateTimeOffset.FromUnixTimeMilliseconds(parsedEnd))
                : (0, DateTimeOffset.MinValue);
    }
}
