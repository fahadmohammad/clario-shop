namespace ClarioShop.Catalog.Features.Images;

/// <summary>
/// An image type the storefront serves, recognised by the file's first bytes. The uploader's content type and file
/// name are ignored: a file served from the shop's own domain must be what it claims to be.
/// </summary>
internal sealed record ImageFormat(string ContentType, string Extension)
{
    public static readonly ImageFormat Jpeg = new("image/jpeg", ".jpg");
    public static readonly ImageFormat Png = new("image/png", ".png");
    public static readonly ImageFormat WebP = new("image/webp", ".webp");
    public static readonly ImageFormat Avif = new("image/avif", ".avif");

    /// <summary>Bytes needed to recognise every supported format.</summary>
    public const int HeaderLength = 12;

    public static ImageFormat? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return Jpeg;
        }

        if (header.StartsWith((ReadOnlySpan<byte>)[0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return Png;
        }

        if (header.Length >= HeaderLength && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return WebP;
        }

        // ISO media file whose major brand is AVIF: a still image ("avif") or an image sequence ("avis").
        return header.Length >= HeaderLength && header[4..8].SequenceEqual("ftyp"u8)
            && (header[8..12].SequenceEqual("avif"u8) || header[8..12].SequenceEqual("avis"u8))
            ? Avif
            : null;
    }
}
