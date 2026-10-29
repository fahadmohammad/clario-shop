namespace ClarioShop.Infrastructure.Storage;

/// <summary>
/// Stores files such as product images under a key. Meant for S3-compatible object storage (Cloudflare R2 or
/// MinIO); until a bucket is configured, files go to the local disk.
/// </summary>
public interface IFileStorage
{
    /// <param name="key">Slash-separated path, e.g. <c>{tenant}/catalog/products/{product}/{image}.webp</c>. Callers make it unique.</param>
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>The file's content, or null when no file has this key. The caller disposes the stream.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);

    /// <summary>Removes the file; a key with no file is not an error.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
