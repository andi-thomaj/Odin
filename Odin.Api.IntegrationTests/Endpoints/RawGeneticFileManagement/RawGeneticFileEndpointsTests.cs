using System.Net;
using System.Net.Http.Json;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.RawGeneticFileManagement.Models;
using Odin.Api.IntegrationTests.Fakers;
using Odin.Api.IntegrationTests.Infrastructure;
using static Odin.Api.IntegrationTests.Fakers.TestDataHelper;

namespace Odin.Api.IntegrationTests.Endpoints.RawGeneticFileManagement;

public class RawGeneticFileEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    // ── GET /api/raw-genetic-files ─────────────────────────────────

    [Fact]
    public async Task GetAllFiles_WhenNoFiles_ReturnsEmptyList()
    {
        var response = await Client.GetAsync("/api/raw-genetic-files");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var files = await response.Content.ReadFromJsonAsync<List<GetGeneticFileContract.Response>>();
        Assert.NotNull(files);
        Assert.Empty(files!);
    }

    [Fact]
    public async Task GetAllFiles_AfterUpload_ReturnsUploadedFiles()
    {
        var file1 = await UploadTestFileAsync(fileName: "file1.txt");
        var file2 = await UploadTestFileAsync(fileName: "file2.csv");

        var response = await Client.GetAsync("/api/raw-genetic-files");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var files = await response.Content.ReadFromJsonAsync<List<GetGeneticFileContract.Response>>();
        Assert.NotNull(files);
        Assert.Equal(2, files!.Count);
        Assert.Contains(files, f => f.FileName == "file1.txt");
        Assert.Contains(files, f => f.FileName == "file2.csv");
    }

    [Fact]
    public async Task GetAllFiles_DifferentUser_DoesNotSeeOtherUsersFiles()
    {
        var uploaded = await UploadTestFileAsync(fileName: "other-user-isolation.txt");

        var otherUser = UserFaker.GenerateCreateRequest();
        await Client.PostAsJsonAsync("/api/users", otherUser);
        using var otherClient = CreateClientWithRole(Factory, otherUser.IdentityId, "User");

        var response = await otherClient.GetAsync("/api/raw-genetic-files");
        var files = await response.Content.ReadFromJsonAsync<List<GetGeneticFileContract.Response>>();

        Assert.NotNull(files);
        Assert.DoesNotContain(files!, f => f.Id == uploaded.Id);
    }

    // ── POST /api/raw-genetic-files ────────────────────────────────

    [Fact]
    public async Task UploadFile_TxtFile_ReturnsCreated()
    {
        var fileContent = "Sample genetic data content for testing purposes"u8.ToArray();
        var result = await UploadTestFileAsync(fileContent, "test_genetic_data.txt");

        Assert.Equal("test_genetic_data.txt", result.FileName);
        Assert.Equal(fileContent.Length, result.FileSize);
        Assert.True(result.Id > 0);
    }

    [Fact]
    public async Task UploadFile_CsvFile_ReturnsCreated()
    {
        var fileContent = "rsid,chromosome,position,genotype\nrs1,1,1,AA\n"u8.ToArray();
        var result = await UploadTestFileAsync(fileContent, "data.csv");

        Assert.Equal("data.csv", result.FileName);
    }

    [Fact]
    public async Task UploadFile_ZipFile_ReturnsCreated()
    {
        var zipHeader = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var zipContent = new byte[128];
        Array.Copy(zipHeader, zipContent, 4);
        var result = await UploadTestFileAsync(zipContent, "archive.zip");

        Assert.Equal("archive.zip", result.FileName);
    }

    [Fact]
    public async Task UploadFile_InvalidExtension_ReturnsBadRequest()
    {
        var fileContent = "Invalid file content"u8.ToArray();
        using var content = new MultipartFormDataContent();
        var fileStreamContent = new ByteArrayContent(fileContent);
        fileStreamContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileStreamContent, "file", "invalid_file.exe");

        var response = await Client.PostAsync("/api/raw-genetic-files", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadFile_AsUserRole_ReturnsForbidden()
    {
        using var userClient = CreateClientWithRole(Factory, "auth0|integration-default", "User");

        var fileContent = "data"u8.ToArray();
        using var content = new MultipartFormDataContent();
        var filePart = new ByteArrayContent(fileContent);
        filePart.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(filePart, "file", "test.txt");

        var response = await userClient.PostAsync("/api/raw-genetic-files", content);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── GET /api/raw-genetic-files/{id} ────────────────────────────

    [Fact]
    public async Task GetFileById_WhenFileExists_ReturnsFile()
    {
        var uploadedFile = await UploadTestFileAsync();

        var response = await Client.GetAsync($"/api/raw-genetic-files/{uploadedFile.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var file = await response.Content.ReadFromJsonAsync<GetGeneticFileContract.Response>();
        Assert.NotNull(file);
        Assert.Equal(uploadedFile.Id, file!.Id);
        Assert.Equal(uploadedFile.FileName, file.FileName);
    }

    [Fact]
    public async Task GetFileById_WhenNotExists_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/api/raw-genetic-files/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetFileById_NonOwner_ReturnsNotFound()
    {
        var uploaded = await UploadTestFileAsync();

        var otherUser = UserFaker.GenerateCreateRequest();
        await Client.PostAsJsonAsync("/api/users", otherUser);
        using var otherClient = CreateClientWithRole(Factory, otherUser.IdentityId, "User");

        var response = await otherClient.GetAsync($"/api/raw-genetic-files/{uploaded.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── GET /api/raw-genetic-files/{id}/download ───────────────────

    [Fact]
    public async Task DownloadFile_WhenExists_ReturnsFileContent()
    {
        var fileContent = "Downloadable genetic data"u8.ToArray();
        var uploadedFile = await UploadTestFileAsync(fileContent, "download_test.csv");

        var response = await Client.GetAsync($"/api/raw-genetic-files/{uploadedFile.Id}/download");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
        var downloadedContent = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(fileContent, downloadedContent);
    }

    [Fact]
    public async Task DownloadFile_WhenNotExists_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/api/raw-genetic-files/99999/download");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DownloadFile_NonOwner_ReturnsForbidden()
    {
        var uploaded = await UploadTestFileAsync();

        var otherUser = UserFaker.GenerateCreateRequest();
        await Client.PostAsJsonAsync("/api/users", otherUser);
        using var otherClient = CreateClientWithRole(Factory, otherUser.IdentityId, "User");

        var response = await otherClient.GetAsync($"/api/raw-genetic-files/{uploaded.Id}/download");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── DELETE /api/raw-genetic-files/{id} ──────────────────────────

    [Fact]
    public async Task DeleteFile_WhenExists_ReturnsNoContent()
    {
        var uploadedFile = await UploadTestFileAsync();

        var response = await Client.DeleteAsync($"/api/raw-genetic-files/{uploadedFile.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await Client.GetAsync($"/api/raw-genetic-files/{uploadedFile.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteFile_WhenNotExists_ReturnsNotFound()
    {
        var response = await Client.DeleteAsync("/api/raw-genetic-files/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteFile_NonOwner_ReturnsForbidden()
    {
        var uploaded = await UploadTestFileAsync();

        var otherUser = UserFaker.GenerateCreateRequest();
        await Client.PostAsJsonAsync("/api/users", otherUser);
        using var otherClient = CreateClientWithRole(Factory, otherUser.IdentityId, "User");

        var response = await otherClient.DeleteAsync($"/api/raw-genetic-files/{uploaded.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteFile_LinkedToPendingOrder_ReturnsBadRequest()
    {
        var fileId = await SeedRawGeneticFileAsync(Factory.Services, "linked.txt", "auth0|integration-default");
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services);

        using var orderContent = new MultipartFormDataContent();
        orderContent.Add(new StringContent("Test"), "FirstName");
        orderContent.Add(new StringContent("User"), "LastName");
        orderContent.Add(new StringContent("Male"), "Gender");
        orderContent.Add(new StringContent("0"), "Service");
        foreach (var rid in regionIds)
            orderContent.Add(new StringContent(rid.ToString()), "RegionIds");
        orderContent.Add(new StringContent(fileId.ToString()), "ExistingFileId");

        var orderResponse = await Client.PostAsync("/api/orders", orderContent);
        orderResponse.EnsureSuccessStatusCode();

        var response = await Client.DeleteAsync($"/api/raw-genetic-files/{fileId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Helpers ────────────────────────────────────────────────────

    private async Task<UploadGeneticFileContract.Response> UploadTestFileAsync(
        byte[]? fileContent = null,
        string fileName = "test_file.txt")
    {
        fileContent ??= "Test genetic data content"u8.ToArray();

        using var content = new MultipartFormDataContent();
        var fileStreamContent = new ByteArrayContent(fileContent);
        fileStreamContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileStreamContent, "file", fileName);

        var response = await Client.PostAsync("/api/raw-genetic-files", content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UploadGeneticFileContract.Response>();
        return result!;
    }
}
