using System.Security.Cryptography.X509Certificates;
using ClarioShop.Platform.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;

namespace ClarioShop.Platform.Services;

internal static class DataProtectionRegistration
{
    public const string ApplicationName = "ClarioShop";

    /// <summary>
    /// One Data Protection key ring for every instance of the API, kept in the platform database, so a value one
    /// instance protects survives restarts and can be read by the others. Outside Development and Testing the keys
    /// are encrypted with a certificate, so a copy of the database alone cannot unprotect anything.
    /// </summary>
    public static void AddPlatformDataProtection(this IServiceCollection services, PlatformOptions options, IHostEnvironment environment)
    {
        var dataProtection = services.AddDataProtection()
            .SetApplicationName(ApplicationName)
            .PersistKeysToDbContext<PlatformDbContext>();

        if (!string.IsNullOrEmpty(options.DataProtectionCertificate))
        {
            var certificate = X509CertificateLoader.LoadPkcs12(
                Convert.FromBase64String(options.DataProtectionCertificate),
                options.DataProtectionCertificatePassword,
                X509KeyStorageFlags.EphemeralKeySet);

            // The certificate is not in a certificate store, so it is also handed over for decrypting the keys.
            dataProtection.ProtectKeysWithCertificate(certificate).UnprotectKeysWithAnyCertificate(certificate);
        }
        else if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            throw new InvalidOperationException("Platform:DataProtectionCertificate must be configured.");
        }
    }
}
