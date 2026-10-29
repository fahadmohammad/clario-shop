namespace ClarioShop.Infrastructure.Storage;

/// <summary>
/// Stand-in for object storage in development and tests: one file per key under <see cref="FileStorageOptions.LocalRootPath"/>.
/// Not for production, where several app servers would each see only their own disk.
/// </summary>
internal sealed class LocalDiskFileStorage(IOptions<FileStorageOptions> options) : IFileStorage
{
    private readonly string root = Path.GetFullPath(options.Value.LocalRootPath) + Path.DirectorySeparatorChar;

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <summary>Keys come from our own code, but a key must still never reach outside the root folder.</summary>
    private string PathFor(string key)
    {
        var path = Path.GetFullPath(Path.Combine(root, key));
        return path.StartsWith(root, StringComparison.Ordinal)
            ? path
            : throw new ArgumentException($"The key {key} points outside the storage folder.", nameof(key));
    }
}
