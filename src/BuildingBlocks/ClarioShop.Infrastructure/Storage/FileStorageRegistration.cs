using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClarioShop.Infrastructure.Storage;

public static class FileStorageRegistration
{
    /// <summary>The local-disk file storage, unless an object storage <see cref="IFileStorage"/> is already registered.</summary>
    public static IServiceCollection AddClarioFileStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.TryAddSingleton<IFileStorage, LocalDiskFileStorage>();
        return services;
    }
}
