using System.Text;
using ClarioShop.Catalog.Features.Images;

namespace ClarioShop.Catalog.Tests.Domain;

public sealed class ImageFormatTests
{
    public static TheoryData<byte[], string> Images => new()
    {
        { [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1], "image/jpeg" },
        { [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D], "image/png" },
        { [.. "RIFF"u8, 0x24, 0, 0, 0, .. "WEBP"u8], "image/webp" },
        { [0, 0, 0, 0x1C, .. "ftyp"u8, .. "avif"u8], "image/avif" },
    };

    [Theory]
    [MemberData(nameof(Images))]
    public void Detect_SupportedImage_ReturnsItsContentType(byte[] header, string contentType)
    {
        var format = ImageFormat.Detect(header);

        Assert.Equal(contentType, format?.ContentType);
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"/>")]
    [InlineData("<html><script>alert(1)</script>")]
    [InlineData("GIF89a......")]
    [InlineData("RIFF....WAVE")]
    [InlineData("")]
    public void Detect_AnythingElse_ReturnsNull(string content)
    {
        var format = ImageFormat.Detect(Encoding.ASCII.GetBytes(content));

        Assert.Null(format);
    }
}
