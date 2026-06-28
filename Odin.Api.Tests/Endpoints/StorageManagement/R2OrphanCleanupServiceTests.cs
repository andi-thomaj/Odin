using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.StorageManagement;
using Odin.Api.Storage;

namespace Odin.Api.Tests.Endpoints.StorageManagement;

public class R2OrphanCleanupServiceTests
{
    // "google-oauth2|live" sanitises to "google-oauth2_live"; the orphan's user is NOT in the DB.
    private const string LiveIdentity = "google-oauth2|live";
    private const string OrphanIdentity = "auth0|deleted-user";

    [Fact]
    public async Task RunAsync_deletes_orphan_user_data_and_keeps_live_users()
    {
        await using var db = CreateDbContext();
        db.Users.Add(new User { IdentityId = LiveIdentity, CreatedBy = "test" });
        await db.SaveChangesAsync();

        var r2 = new InMemoryR2();
        var liveSlug = UserStorageKeys.Sanitize(LiveIdentity);
        var orphanSlug = UserStorageKeys.Sanitize(OrphanIdentity);
        await Seed(r2, $"users/{liveSlug}/face-photos/a.jpg");
        await Seed(r2, $"users/{liveSlug}/ancestral-portraits/set1/0-0.jpg");
        await Seed(r2, $"users/{orphanSlug}/face-photos/b.jpg");
        await Seed(r2, $"users/{orphanSlug}/ancestral-portraits/set2/0-0.jpg");

        var service = new R2OrphanCleanupService(db, r2, NullLogger<R2OrphanCleanupService>.Instance);
        var report = await service.RunAsync(dryRun: false);

        Assert.False(report.DryRun);
        Assert.Equal(2, report.ScannedUserSlugs);
        Assert.Equal(1, report.LiveUsers);
        Assert.Equal(new[] { orphanSlug }, report.OrphanSlugs);
        Assert.Equal(2, report.DeletedObjects);

        // The orphan's objects are gone; the live user's remain.
        Assert.Empty(await r2.ListKeysAsync($"users/{orphanSlug}/"));
        Assert.Equal(2, (await r2.ListKeysAsync($"users/{liveSlug}/")).Count);
    }

    [Fact]
    public async Task RunAsync_dry_run_reports_but_deletes_nothing()
    {
        await using var db = CreateDbContext();
        db.Users.Add(new User { IdentityId = LiveIdentity, CreatedBy = "test" }); // a live user → abort guard won't fire
        await db.SaveChangesAsync();
        var r2 = new InMemoryR2();
        var orphanSlug = UserStorageKeys.Sanitize(OrphanIdentity);
        await Seed(r2, $"users/{UserStorageKeys.Sanitize(LiveIdentity)}/face-photos/a.jpg");
        await Seed(r2, $"users/{orphanSlug}/face-photos/b.jpg");

        var service = new R2OrphanCleanupService(db, r2, NullLogger<R2OrphanCleanupService>.Instance);
        var report = await service.RunAsync(dryRun: true);

        Assert.True(report.DryRun);
        Assert.Equal(new[] { orphanSlug }, report.OrphanSlugs);
        Assert.Equal(1, report.DeletedObjects);                       // what WOULD be deleted
        Assert.Single(await r2.ListKeysAsync($"users/{orphanSlug}/")); // still there
    }

    [Fact]
    public async Task RunAsync_keeps_user_whose_identity_has_special_chars()
    {
        await using var db = CreateDbContext();
        db.Users.Add(new User { IdentityId = LiveIdentity, CreatedBy = "test" });
        await db.SaveChangesAsync();

        var r2 = new InMemoryR2();
        await Seed(r2, $"users/{UserStorageKeys.Sanitize(LiveIdentity)}/face-photos/a.jpg");

        var service = new R2OrphanCleanupService(db, r2, NullLogger<R2OrphanCleanupService>.Instance);
        var report = await service.RunAsync(dryRun: false);

        Assert.Empty(report.OrphanSlugs);
        Assert.Equal(0, report.DeletedObjects);
    }

    [Fact]
    public async Task RunAsync_aborts_without_deleting_when_db_has_zero_users_but_r2_has_data()
    {
        await using var db = CreateDbContext(); // NO users — simulates a wrong/empty DB connection
        var r2 = new InMemoryR2();
        await Seed(r2, $"users/{UserStorageKeys.Sanitize(LiveIdentity)}/face-photos/a.jpg");
        await Seed(r2, $"users/{UserStorageKeys.Sanitize(OrphanIdentity)}/face-photos/b.jpg");

        var service = new R2OrphanCleanupService(db, r2, NullLogger<R2OrphanCleanupService>.Instance);
        var report = await service.RunAsync(dryRun: false);

        Assert.True(report.Aborted);
        Assert.Equal(0, report.DeletedObjects);
        Assert.Equal(2, (await r2.ListKeysAsync("users/")).Count); // nothing deleted
    }

    [Fact]
    public async Task RunAsync_aborts_on_mass_orphan_ratio()
    {
        // 30 R2 user slugs but only 5 live users → 25/30 (>50%) orphaned ⇒ the DB side looks wrong, refuse to delete.
        await using var db = CreateDbContext();
        var r2 = new InMemoryR2();
        for (var i = 0; i < 5; i++)
        {
            var id = $"auth0|live-{i}";
            db.Users.Add(new User { IdentityId = id, CreatedBy = "test" });
            await Seed(r2, $"users/{UserStorageKeys.Sanitize(id)}/face-photos/a.jpg");
        }
        await db.SaveChangesAsync();
        for (var i = 0; i < 25; i++)
            await Seed(r2, $"users/{UserStorageKeys.Sanitize($"auth0|gone-{i}")}/face-photos/a.jpg");

        var service = new R2OrphanCleanupService(db, r2, NullLogger<R2OrphanCleanupService>.Instance);
        var report = await service.RunAsync(dryRun: false);

        Assert.True(report.Aborted);
        Assert.Equal(0, report.DeletedObjects);
        Assert.Equal(30, (await r2.ListKeysAsync("users/")).Count); // nothing deleted
    }

    private static async Task Seed(InMemoryR2 r2, string key)
    {
        using var ms = new MemoryStream(new byte[] { 1, 2, 3 });
        await r2.UploadAsync(key, ms, "image/jpeg");
    }

    private static ApplicationDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"r2-cleanup-tests-{Guid.NewGuid():N}").Options);

    /// Minimal in-memory IR2Storage with S3-like prefix/delimiter listing.
    private sealed class InMemoryR2 : IR2Storage
    {
        private readonly ConcurrentDictionary<string, byte[]> _objects = new();

        public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            _objects[key] = ms.ToArray();
        }

        public Task DeleteAsync(string key, CancellationToken ct = default)
        {
            _objects.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        public Task<byte[]?> DownloadAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(_objects.TryGetValue(key, out var b) ? b : null);

        public Task CopyAsync(string sourceKey, string destinationKey, CancellationToken ct = default) =>
            Task.CompletedTask;

        public string GetPublicUrl(string key) => $"https://test/{key}";

        public Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct = default)
        {
            IReadOnlyList<string> keys = _objects.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
            return Task.FromResult(keys);
        }

        public Task<IReadOnlyList<string>> ListCommonPrefixesAsync(string prefix, string delimiter = "/", CancellationToken ct = default)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in _objects.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
            {
                var remainder = key[prefix.Length..];
                var idx = remainder.IndexOf(delimiter, StringComparison.Ordinal);
                if (idx >= 0) set.Add(prefix + remainder[..(idx + delimiter.Length)]);
            }
            IReadOnlyList<string> result = set.ToList();
            return Task.FromResult(result);
        }

        public async Task DeleteManyAsync(IReadOnlyCollection<string> keys, CancellationToken ct = default)
        {
            foreach (var key in keys) await DeleteAsync(key, ct);
        }
    }
}
