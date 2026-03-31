using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.NotificationManagement.Models;
using Odin.Api.Endpoints.ReportManagement.Models;
using Odin.Api.IntegrationTests.Fakers;
using Odin.Api.IntegrationTests.Infrastructure;
using static Odin.Api.IntegrationTests.Fakers.TestDataHelper;

namespace Odin.Api.IntegrationTests.Endpoints.ReportManagement;

public class ReportEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    // ── GET /api/reports ───────────────────────────────────────────

    [Fact]
    public async Task GetAll_AsAdmin_ReturnsOk()
    {
        var response = await Client.GetAsync("/api/reports");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<ReportListContract.ListItem>>();
        Assert.NotNull(list);
    }

    [Fact]
    public async Task GetAll_AsUser_ReturnsOnlyOwnReports()
    {
        var user1 = UserFaker.GenerateCreateRequest();
        await Client.PostAsJsonAsync("/api/users", user1);
        using var user1Client = CreateClientWithRole(Factory, user1.IdentityId, "User");

        using var form1 = ReportFaker.GenerateCreateForm(subject: "User1 Report");
        await user1Client.PostAsync("/api/reports", form1);

        var user2 = UserFaker.GenerateCreateRequest();
        await Client.PostAsJsonAsync("/api/users", user2);
        using var user2Client = CreateClientWithRole(Factory, user2.IdentityId, "User");

        using var form2 = ReportFaker.GenerateCreateForm(subject: "User2 Report");
        await user2Client.PostAsync("/api/reports", form2);

        var response = await user1Client.GetAsync("/api/reports");
        var list = await response.Content.ReadFromJsonAsync<List<ReportListContract.ListItem>>();

        Assert.NotNull(list);
        Assert.All(list!, item => Assert.Equal("User1 Report", item.Subject));
    }

    [Fact]
    public async Task GetAll_AsAdmin_WithTypeFilter_FiltersCorrectly()
    {
        await PromoteDefaultUserToAdminAsync();

        using var bugForm = ReportFaker.GenerateCreateForm(type: "Bug", subject: "Bug Report");
        await Client.PostAsync("/api/reports", bugForm);

        using var featureForm = ReportFaker.GenerateCreateForm(type: "FeatureRequest", subject: "Feature Report");
        await Client.PostAsync("/api/reports", featureForm);

        var response = await Client.GetAsync("/api/reports?type=Bug");
        var list = await response.Content.ReadFromJsonAsync<List<ReportListContract.ListItem>>();

        Assert.NotNull(list);
        Assert.Single(list!);
        Assert.All(list, item => Assert.Equal("Bug", item.Type));
    }

    // ── POST /api/reports ──────────────────────────────────────────

    [Fact]
    public async Task Create_WithValidForm_ReturnsCreated()
    {
        using var content = ReportFaker.GenerateCreateForm(type: "Bug", subject: "Test subject");

        var response = await Client.PostAsync("/api/reports", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateReportContract.Response>();
        Assert.NotNull(body);
        Assert.True(body!.Id > 0);
        Assert.Equal("Bug", body.Type);
        Assert.Equal("Test subject", body.Subject);
        Assert.Equal("Pending", body.Status);
    }

    [Fact]
    public async Task Create_WithFileAttachment_StoresFile()
    {
        var pngBytes = ReportFaker.GeneratePngBytes();
        using var content = ReportFaker.GenerateCreateForm(
            fileBytes: pngBytes,
            fileName: "screenshot.png",
            fileContentType: "image/png");

        var response = await Client.PostAsync("/api/reports", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateReportContract.Response>();
        Assert.NotNull(body);
        Assert.Equal("screenshot.png", body!.FileName);
    }

    [Fact]
    public async Task Create_WithInvalidFileType_ReturnsBadRequest()
    {
        var exeBytes = new byte[64];
        Random.Shared.NextBytes(exeBytes);
        using var content = ReportFaker.GenerateCreateForm(
            fileBytes: exeBytes,
            fileName: "malware.exe",
            fileContentType: "application/octet-stream");

        var response = await Client.PostAsync("/api/reports", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_NotifiesAdmins()
    {
        await PromoteDefaultUserToAdminAsync();

        using var content = ReportFaker.GenerateCreateForm(subject: "Notify Test");
        var response = await Client.PostAsync("/api/reports", content);
        response.EnsureSuccessStatusCode();

        var notifResponse = await Client.GetAsync("/api/notifications");
        var notifications = await notifResponse.Content.ReadFromJsonAsync<List<GetNotificationContract.Response>>();
        Assert.NotNull(notifications);
        Assert.Contains(notifications!, n => n.Type == "NewReport" && n.Message.Contains("Notify Test"));
    }

    // ── GET /api/reports/{id} ──────────────────────────────────────

    [Fact]
    public async Task GetDetail_AsAdmin_ReturnsAnyReport()
    {
        await PromoteDefaultUserToAdminAsync();

        var user = UserFaker.GenerateCreateRequest();
        await Client.PostAsJsonAsync("/api/users", user);
        using var userClient = CreateClientWithRole(Factory, user.IdentityId, "User");

        using var form = ReportFaker.GenerateCreateForm(subject: "Detail Test");
        var createResponse = await userClient.PostAsync("/api/reports", form);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateReportContract.Response>();

        var response = await Client.GetAsync($"/api/reports/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<ReportListContract.Detail>();
        Assert.NotNull(detail);
        Assert.Equal("Detail Test", detail!.Subject);
    }

    [Fact]
    public async Task GetDetail_AsUser_CannotSeeOtherUsersReport()
    {
        using var form = ReportFaker.GenerateCreateForm();
        var createResponse = await Client.PostAsync("/api/reports", form);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateReportContract.Response>();

        var otherUser = UserFaker.GenerateCreateRequest();
        await Client.PostAsJsonAsync("/api/users", otherUser);
        using var otherClient = CreateClientWithRole(Factory, otherUser.IdentityId, "User");

        var response = await otherClient.GetAsync($"/api/reports/{created!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDetail_NonExistent_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/api/reports/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── PATCH /api/reports/{id}/status ──────────────────────────────

    [Fact]
    public async Task UpdateStatus_AsAdmin_Succeeds()
    {
        using var form = ReportFaker.GenerateCreateForm();
        var createResponse = await Client.PostAsync("/api/reports", form);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateReportContract.Response>();

        var response = await Client.PatchAsJsonAsync(
            $"/api/reports/{created!.Id}/status",
            new UpdateReportStatusContract.Request { Status = "InReview", AdminNotes = "Looking into it" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UpdateReportStatusContract.Response>();
        Assert.NotNull(result);
        Assert.Equal("InReview", result!.Status);
        Assert.Equal("Looking into it", result.AdminNotes);
    }

    [Fact]
    public async Task UpdateStatus_AsNonAdmin_ReturnsForbidden()
    {
        using var form = ReportFaker.GenerateCreateForm();
        var createResponse = await Client.PostAsync("/api/reports", form);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateReportContract.Response>();

        using var userClient = CreateClientWithRole(Factory, "auth0|integration-default", "User");
        var response = await userClient.PatchAsJsonAsync(
            $"/api/reports/{created!.Id}/status",
            new UpdateReportStatusContract.Request { Status = "Resolved" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_InvalidStatus_ReturnsBadRequest()
    {
        using var form = ReportFaker.GenerateCreateForm();
        var createResponse = await Client.PostAsync("/api/reports", form);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateReportContract.Response>();

        var response = await Client.PatchAsJsonAsync(
            $"/api/reports/{created!.Id}/status",
            new UpdateReportStatusContract.Request { Status = "NonExistentStatus" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_NotifiesReportOwner()
    {
        var user = UserFaker.GenerateCreateRequest();
        await Client.PostAsJsonAsync("/api/users", user);
        using var userClient = CreateClientWithRole(Factory, user.IdentityId, "User");

        using var form = ReportFaker.GenerateCreateForm(subject: "Status Notify Test");
        var createResponse = await userClient.PostAsync("/api/reports", form);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateReportContract.Response>();

        await Client.PatchAsJsonAsync(
            $"/api/reports/{created!.Id}/status",
            new UpdateReportStatusContract.Request { Status = "Resolved" });

        var notifResponse = await userClient.GetAsync("/api/notifications");
        var notifications = await notifResponse.Content.ReadFromJsonAsync<List<GetNotificationContract.Response>>();
        Assert.NotNull(notifications);
        Assert.Contains(notifications!, n =>
            n.Type == "ReportStatusUpdated" && n.Message.Contains("Status Notify Test"));
    }

    // ── GET /api/reports/{id}/file ─────────────────────────────────

    [Fact]
    public async Task DownloadFile_AsAdmin_ReturnsFile()
    {
        var pngBytes = ReportFaker.GeneratePngBytes(256);
        using var form = ReportFaker.GenerateCreateForm(
            fileBytes: pngBytes,
            fileName: "test.png",
            fileContentType: "image/png");
        var createResponse = await Client.PostAsync("/api/reports", form);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateReportContract.Response>();

        var response = await Client.GetAsync($"/api/reports/{created!.Id}/file");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(pngBytes.Length, bytes.Length);
    }

    [Fact]
    public async Task DownloadFile_NoFile_ReturnsNotFound()
    {
        using var form = ReportFaker.GenerateCreateForm();
        var createResponse = await Client.PostAsync("/api/reports", form);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateReportContract.Response>();

        var response = await Client.GetAsync($"/api/reports/{created!.Id}/file");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task PromoteDefaultUserToAdminAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.FirstAsync(u => u.IdentityId == "auth0|integration-default");
        user.Role = AppRole.Admin;
        await db.SaveChangesAsync();
    }
}
