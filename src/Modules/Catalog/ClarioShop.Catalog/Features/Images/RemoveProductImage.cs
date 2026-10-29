using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Features.Products;
using ClarioShop.Catalog.Persistence;
using ClarioShop.Infrastructure.Storage;
using Microsoft.Extensions.Logging;

namespace ClarioShop.Catalog.Features.Images;

/// <summary>
/// Removes a photo from a product's gallery. The row goes first and the file after, so the gallery never shows a
/// missing file; a file that cannot be removed is only logged.
/// </summary>
public static partial class RemoveProductImage
{
    public sealed record Command(Guid ProductId, Guid ImageId);

    internal sealed partial class Handler(CatalogDbContext db, IFileStorage storage, ILogger<Handler> logger)
        : ICommandHandler<Command, IReadOnlyList<ImageDetails>>
    {
        public async Task<Result<IReadOnlyList<ImageDetails>>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var product = await db.LoadProductAsync(command.ProductId, cancellationToken);
            if (product is null)
            {
                return CatalogErrors.ProductNotFound;
            }

            var removed = product.RemoveImage(command.ImageId);
            if (removed.IsFailure)
            {
                return removed.Error;
            }

            var saved = await db.SaveProductAsync(product, cancellationToken);
            if (saved.IsFailure)
            {
                return saved.Error;
            }

            try
            {
                await storage.DeleteAsync(removed.Value.StorageKey, CancellationToken.None);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The image is already gone from the product; failing the request now would only make staff retry
                // a removal that has happened. The file is left behind for a later cleanup.
                LogFileLeftBehind(logger, exception, removed.Value.StorageKey);
            }

            return Result.Success(ImageDetails.ListOf(product));
        }

        [LoggerMessage(Level = LogLevel.Warning, Message = "Removed image's file {StorageKey} could not be deleted from file storage")]
        private static partial void LogFileLeftBehind(ILogger logger, Exception exception, string storageKey);
    }
}
