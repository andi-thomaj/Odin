using Odin.Api.Endpoints.ImageGenerationManagement;

namespace Odin.Api.Tests.Endpoints.ImageGenerationManagement;

public class ImageParameterVocabularyTests
{
    [Theory]
    [InlineData(null)]            // omitted → defaults apply
    [InlineData("auto")]
    [InlineData("1024x1024")]    // square preset
    [InlineData("1536x1024")]    // landscape preset
    [InlineData("1024x1536")]    // portrait preset
    [InlineData("2048x1152")]    // custom 16:9, multiples of 16, within pixel range
    [InlineData("3840x2160")]    // gpt-image-2 max edge + max pixels
    public void ValidateSize_AcceptsAutoAndValidDimensions(string? size)
    {
        Assert.Null(ImageParameterVocabulary.ValidateSize(size));
    }

    [Theory]
    [InlineData("999x999")]      // not multiples of 16
    [InlineData("1000x1000")]    // not multiples of 16
    [InlineData("3856x2160")]    // edge > 3840
    [InlineData("512x512")]      // below the 655,360 minimum pixel count
    [InlineData("3840x1024")]    // aspect ratio > 3:1
    [InlineData("notasize")]     // unparseable
    [InlineData("1024")]         // missing height
    public void ValidateSize_RejectsInvalidDimensions(string size)
    {
        Assert.NotNull(ImageParameterVocabulary.ValidateSize(size));
    }
}
