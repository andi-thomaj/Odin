using System.Net;
using System.Net.Http.Json;
using System.Text;
using Odin.Api.Endpoints.RawGeneticFileManagement.Models;
using Odin.Api.IntegrationTests.Infrastructure;

namespace Odin.Api.IntegrationTests.Endpoints.RawGeneticFileManagement;

public class RawGeneticFileEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetAllFiles_WhenNoFiles_ReturnsEmptyList()
    {
        // Act
        var response = await Client.GetAsync("/api/raw-genetic-files");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var files = await response.Content.ReadFromJsonAsync<List<GetGeneticFileContract.Response>>();
        Assert.NotNull(files);
        Assert.Empty(files);
    }

    [Fact]
    public async Task UploadFile_WithValidFile_ReturnsCreated()
    {
        // Arrange
        var fileContent = "Sample genetic data content for testing purposes"u8.ToArray();
        using var content = new MultipartFormDataContent();
        using var fileStreamContent = new ByteArrayContent(fileContent);
        fileStreamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileStreamContent, "file", "test_genetic_data.txt");

        // Act
        var response = await Client.PostAsync("/api/raw-genetic-files", content);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<UploadGeneticFileContract.Response>();
        Assert.NotNull(result);
        Assert.Equal("test_genetic_data.txt", result.FileName);
        Assert.Equal(fileContent.Length, result.FileSize);
        Assert.True(result.Id > 0);
    }

    [Fact]
    public async Task UploadFile_WithInvalidExtension_ReturnsBadRequest()
    {
        // Arrange
        var fileContent = "Invalid file content"u8.ToArray();
        using var content = new MultipartFormDataContent();
        using var fileStreamContent = new ByteArrayContent(fileContent);
        fileStreamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileStreamContent, "file", "invalid_file.exe");

        // Act
        var response = await Client.PostAsync("/api/raw-genetic-files", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetFileById_WhenFileExists_ReturnsFile()
    {
        // Arrange
        var uploadedFile = await UploadTestFileAsync();

        // Act
        var response = await Client.GetAsync($"/api/raw-genetic-files/{uploadedFile.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var file = await response.Content.ReadFromJsonAsync<GetGeneticFileContract.Response>();
        Assert.NotNull(file);
        Assert.Equal(uploadedFile.Id, file.Id);
        Assert.Equal(uploadedFile.FileName, file.FileName);
    }

    [Fact]
    public async Task GetFileById_WhenFileDoesNotExist_ReturnsNotFound()
    {
        // Act
        var response = await Client.GetAsync("/api/raw-genetic-files/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DownloadFile_WhenFileExists_ReturnsFileContent()
    {
        // Arrange
        var fileContent = "Downloadable genetic data"u8.ToArray();
        var uploadedFile = await UploadTestFileAsync(fileContent, "download_test.csv");

        // Act
        var response = await Client.GetAsync($"/api/raw-genetic-files/{uploadedFile.Id}/download");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);

        var downloadedContent = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(fileContent, downloadedContent);
    }

    [Fact]
    public async Task DownloadFile_WhenFileDoesNotExist_ReturnsNotFound()
    {
        // Act
        var response = await Client.GetAsync("/api/raw-genetic-files/99999/download");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteFile_WhenFileExists_ReturnsNoContent()
    {
        // Arrange
        var uploadedFile = await UploadTestFileAsync();

        // Act
        var response = await Client.DeleteAsync($"/api/raw-genetic-files/{uploadedFile.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify file is deleted
        var getResponse = await Client.GetAsync($"/api/raw-genetic-files/{uploadedFile.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteFile_WhenFileDoesNotExist_ReturnsNotFound()
    {
        // Act
        var response = await Client.DeleteAsync("/api/raw-genetic-files/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAllFiles_AfterUpload_ReturnsUploadedFiles()
    {
        // Arrange
        var file1 = await UploadTestFileAsync(fileName: "file1.txt");
        var file2 = await UploadTestFileAsync(fileName: "file2.csv");

        // Act
        var response = await Client.GetAsync("/api/raw-genetic-files");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var files = await response.Content.ReadFromJsonAsync<List<GetGeneticFileContract.Response>>();
        Assert.NotNull(files);
        Assert.Equal(2, files.Count);
        Assert.Contains(files, f => f.FileName == "file1.txt");
        Assert.Contains(files, f => f.FileName == "file2.csv");
    }

    private async Task<UploadGeneticFileContract.Response> UploadTestFileAsync(
        byte[]? fileContent = null,
        string fileName = "test_file.txt")
    {
        fileContent ??= "Test genetic data content"u8.ToArray();

        using var content = new MultipartFormDataContent();
        using var fileStreamContent = new ByteArrayContent(fileContent);
        fileStreamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileStreamContent, "file", fileName);

        var response = await Client.PostAsync("/api/raw-genetic-files", content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UploadGeneticFileContract.Response>();
        return result!;
    }
}
