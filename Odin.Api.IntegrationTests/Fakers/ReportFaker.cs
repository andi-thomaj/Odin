using Bogus;

namespace Odin.Api.IntegrationTests.Fakers;

public static class ReportFaker
{
    private static readonly Faker Faker = new();

    public static MultipartFormDataContent GenerateCreateForm(
        string type = "Bug",
        string? subject = null,
        string? description = null,
        string? pageUrl = null,
        byte[]? fileBytes = null,
        string? fileName = null,
        string? fileContentType = null)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(type), "type");
        content.Add(new StringContent(subject ?? Faker.Lorem.Sentence()), "subject");
        content.Add(new StringContent(description ?? Faker.Lorem.Paragraph()), "description");

        if (pageUrl is not null)
            content.Add(new StringContent(pageUrl), "pageUrl");

        if (fileBytes is not null && fileName is not null)
        {
            var filePart = new ByteArrayContent(fileBytes);
            filePart.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(fileContentType ?? "image/png");
            content.Add(filePart, "file", fileName);
        }

        return content;
    }

    public static byte[] GeneratePngBytes(int size = 64)
    {
        var bytes = new byte[size];
        bytes[0] = 0x89; // PNG magic header bytes
        bytes[1] = 0x50;
        bytes[2] = 0x4E;
        bytes[3] = 0x47;
        Random.Shared.NextBytes(bytes.AsSpan(4));
        return bytes;
    }
}
