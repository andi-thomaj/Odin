using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.ChangelogManagement;
using Odin.Api.Endpoints.ChangelogManagement.Models;

namespace Odin.Api.Tests.ChangelogManagement;

public class ChangelogServiceTests
{
    [Theory]
    [InlineData("Feature", true)]
    [InlineData("BugFix", true)]
    [InlineData("Improvement", true)]
    [InlineData("Other", false)]
    [InlineData("", false)]
    public void IsValidEntryType_ReturnsExpected(string type, bool expected) =>
        Assert.Equal(expected, ChangelogService.IsValidEntryType(type));

    [Fact]
    public async Task GetPublishedAsync_ExcludesUnpublishedVersions()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;
        db.ChangelogVersions.AddRange(
            new ChangelogVersion
            {
                Version = "1.0.0",
                Title = "Pub",
                ReleasedAt = now,
                IsPublished = true,
                CreatedAt = now,
                CreatedBy = "u",
                UpdatedAt = now,
                UpdatedBy = "u"
            },
            new ChangelogVersion
            {
                Version = "0.9.0",
                Title = "Draft",
                ReleasedAt = now.AddDays(-1),
                IsPublished = false,
                CreatedAt = now,
                CreatedBy = "u",
                UpdatedAt = now,
                UpdatedBy = "u"
            });
        await db.SaveChangesAsync();

        var service = new ChangelogService(db);
        var list = await service.GetPublishedAsync();

        Assert.Single(list);
        Assert.Equal("1.0.0", list[0].Version);
    }

    [Fact]
    public async Task CreateVersionAndEntry_RoundTrips()
    {
        await using var db = CreateDbContext();
        var service = new ChangelogService(db);

        var v = await service.CreateVersionAsync("admin", new CreateVersionContract.Request
        {
            Version = "2.0.0",
            Title = "Release",
            ReleasedAt = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc),
            IsPublished = true
        });

        Assert.True(v.Id > 0);

        var e = await service.CreateEntryAsync(v.Id, "admin", new CreateEntryContract.Request
        {
            Type = "Feature",
            Description = "New thing",
            DisplayOrder = 0
        });

        Assert.NotNull(e);
        Assert.Equal("Feature", e!.Type);

        var published = await service.GetPublishedAsync();
        Assert.Single(published);
        Assert.Single(published[0].Entries);
        Assert.Equal("New thing", published[0].Entries[0].Description);
    }

    [Fact]
    public async Task DeleteVersionAsync_RemovesEntries()
    {
        await using var db = CreateDbContext();
        var service = new ChangelogService(db);

        var v = await service.CreateVersionAsync("a", new CreateVersionContract.Request
        {
            Version = "1.0.0",
            Title = "T",
            ReleasedAt = DateTime.UtcNow,
            IsPublished = true
        });
        await service.CreateEntryAsync(v.Id, "a", new CreateEntryContract.Request
        {
            Type = "BugFix",
            Description = "Fix",
            DisplayOrder = 1
        });

        var ok = await service.DeleteVersionAsync(v.Id);
        Assert.True(ok);
        Assert.Equal(0, await db.ChangelogEntries.CountAsync());
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"changelog-tests-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
