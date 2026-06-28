using Odin.Api.Services;

namespace Odin.Api.Tests.Services;

public class FileSignatureValidatorImageTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M8AAAMBAQDJ/pLvAAAAAElFTkSuQmCC");

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];

    private static readonly byte[] Webp =
        [(byte)'R', (byte)'I', (byte)'F', (byte)'F', 0x00, 0x00, 0x00, 0x00, (byte)'W', (byte)'E', (byte)'B', (byte)'P'];

    [Fact]
    public void Png_IsRecognized()
    {
        Assert.True(FileSignatureValidator.IsPng(Png));
        Assert.True(FileSignatureValidator.IsSupportedImage(Png, out var contentType));
        Assert.Equal("image/png", contentType);
    }

    [Fact]
    public void Jpeg_IsRecognized()
    {
        Assert.True(FileSignatureValidator.IsJpeg(Jpeg));
        Assert.True(FileSignatureValidator.IsSupportedImage(Jpeg, out var contentType));
        Assert.Equal("image/jpeg", contentType);
    }

    [Fact]
    public void Webp_IsRecognized()
    {
        Assert.True(FileSignatureValidator.IsWebp(Webp));
        Assert.True(FileSignatureValidator.IsSupportedImage(Webp, out var contentType));
        Assert.Equal("image/webp", contentType);
    }

    [Fact]
    public void NonImage_IsRejected()
    {
        var garbage = "this is not an image"u8.ToArray();
        Assert.False(FileSignatureValidator.IsPng(garbage));
        Assert.False(FileSignatureValidator.IsJpeg(garbage));
        Assert.False(FileSignatureValidator.IsWebp(garbage));
        Assert.False(FileSignatureValidator.IsSupportedImage(garbage, out var contentType));
        Assert.Equal("application/octet-stream", contentType);
    }

    [Fact]
    public void CrossType_NotConfused()
    {
        // A PNG is not a JPEG or WEBP, and vice versa.
        Assert.False(FileSignatureValidator.IsJpeg(Png));
        Assert.False(FileSignatureValidator.IsWebp(Png));
        Assert.False(FileSignatureValidator.IsPng(Jpeg));
    }
}
