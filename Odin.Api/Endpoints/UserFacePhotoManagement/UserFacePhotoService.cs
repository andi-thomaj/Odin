using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.UserFacePhotoManagement.Models;
using Odin.Api.Extensions;
using Odin.Api.Services;
using Odin.Api.Storage;

namespace Odin.Api.Endpoints.UserFacePhotoManagement;

/// <inheritdoc cref="IUserFacePhotoService"/>
public class UserFacePhotoService(ApplicationDbContext dbContext, IR2Storage r2Storage) : IUserFacePhotoService
{
    public async Task<(FacePhotoSetContract.Response? Response, string? Error)> ReplaceSetAsync(
        IReadOnlyList<IFormFile> photos, Guid captureSessionId, string identityId, CancellationToken cancellationToken = default)
    {
        if (photos.Count == 0)
            return (null, "At least one photo is required.");
        if (photos.Count > UserFacePhotoLimits.MaxPhotos)
            return (null, $"At most {UserFacePhotoLimits.MaxPhotos} photos are allowed.");

        var user = await dbContext.Users.RequireByIdentityAsync(identityId, cancellationToken);

        // Read + validate + dedupe within the batch (SHA-256). All bytes are buffered up front so we never start
        // mutating R2/DB on an invalid set.
        var prepared = new List<PreparedPhoto>();
        var seenHashes = new HashSet<string>();
        long total = 0;
        foreach (var file in photos)
        {
            if (file.Length == 0)
                return (null, "One of the photos is empty.");
            if (file.Length > UserFacePhotoLimits.MaxPhotoBytes)
                return (null, $"A photo exceeds the maximum size of {UserFacePhotoLimits.MaxPhotoBytes} bytes.");
            total += file.Length;
            if (total > UserFacePhotoLimits.MaxTotalBytes)
                return (null, $"The set exceeds the maximum total size of {UserFacePhotoLimits.MaxTotalBytes} bytes.");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);
            var bytes = ms.ToArray();

            if (!FileSignatureValidator.IsJpeg(bytes))
                return (null, "All photos must be JPEG images.");

            var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!seenHashes.Add(sha))
                continue; // drop exact-duplicate frames within the batch

            var (width, height) = ReadJpegDimensions(bytes);
            prepared.Add(new PreparedPhoto(bytes, sha, width, height, SafeFileName(file.FileName)));
        }

        if (prepared.Count == 0)
            return (null, "No valid photos were provided.");

        // Upload the new bytes to R2 FIRST (a crash here leaves only orphaned R2 objects with no DB row — sweepable,
        // and far safer than a DB row pointing at absent bytes).
        var sessionId = captureSessionId == Guid.Empty ? Guid.NewGuid() : captureSessionId;
        var now = DateTime.UtcNow;
        var identitySlug = SanitizeIdentity(user.IdentityId);
        var newRows = new List<UserFacePhoto>(prepared.Count);
        foreach (var photo in prepared)
        {
            var key = $"users/{identitySlug}/face-photos/{Guid.NewGuid():N}.jpg";
            using (var upload = new MemoryStream(photo.Bytes, writable: false))
            {
                await r2Storage.UploadAsync(key, upload, "image/jpeg", cancellationToken);
            }
            newRows.Add(new UserFacePhoto
            {
                UserId = user.Id,
                CaptureSessionId = sessionId,
                R2Key = key,
                OriginalFileName = photo.FileName,
                ContentType = "image/jpeg",
                Width = photo.Width,
                Height = photo.Height,
                ByteSize = photo.Bytes.LongLength,
                Sha256 = photo.Sha256,
                CreatedBy = identityId,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        // Replace-set: swap rows in the DB, commit, THEN best-effort delete the old R2 objects (so a crash never
        // leaves the DB referencing deleted bytes).
        var oldRows = await dbContext.UserFacePhotos.Where(p => p.UserId == user.Id).ToListAsync(cancellationToken);
        var oldKeys = oldRows.Select(r => r.R2Key).ToList();
        dbContext.UserFacePhotos.RemoveRange(oldRows);
        dbContext.UserFacePhotos.AddRange(newRows);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var key in oldKeys)
            await r2Storage.DeleteAsync(key, cancellationToken);

        return (BuildSet(newRows), null);
    }

    public async Task<FacePhotoSetContract.Response> GetSetAsync(string identityId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.RequireByIdentityAsync(identityId, cancellationToken);
        var rows = await dbContext.UserFacePhotos.AsNoTracking()
            .Where(p => p.UserId == user.Id)
            .OrderBy(p => p.Id)
            .ToListAsync(cancellationToken);
        return BuildSet(rows);
    }

    public async Task<(byte[]? Bytes, string? ContentType, int StatusCode)> GetPhotoBytesAsync(
        int id, string identityId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.RequireByIdentityAsync(identityId, cancellationToken);
        var row = await dbContext.UserFacePhotos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (row is null)
            return (null, null, 404);
        if (row.UserId != user.Id)
            return (null, null, 403);

        var bytes = await r2Storage.DownloadAsync(row.R2Key, cancellationToken);
        return bytes is null ? (null, null, 404) : (bytes, row.ContentType, 200);
    }

    public async Task<int> DeletePhotoAsync(int id, string identityId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.RequireByIdentityAsync(identityId, cancellationToken);
        var row = await dbContext.UserFacePhotos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (row is null)
            return 404;
        if (row.UserId != user.Id)
            return 403;

        await r2Storage.DeleteAsync(row.R2Key, cancellationToken);
        dbContext.UserFacePhotos.Remove(row);
        await dbContext.SaveChangesAsync(cancellationToken);
        return 204;
    }

    public async Task<int> ClearSetAsync(string identityId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.RequireByIdentityAsync(identityId, cancellationToken);
        var rows = await dbContext.UserFacePhotos.Where(p => p.UserId == user.Id).ToListAsync(cancellationToken);
        if (rows.Count == 0)
            return 0;

        foreach (var row in rows)
            await r2Storage.DeleteAsync(row.R2Key, cancellationToken);
        dbContext.UserFacePhotos.RemoveRange(rows);
        await dbContext.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    private static FacePhotoSetContract.Response BuildSet(List<UserFacePhoto> rows) => new()
    {
        CaptureSessionId = rows.Count > 0 ? rows[0].CaptureSessionId : null,
        Count = rows.Count,
        Photos = rows.OrderBy(r => r.Id).Select(MapPhoto).ToList(),
    };

    private static FacePhotoContract.Response MapPhoto(UserFacePhoto p) => new()
    {
        Id = p.Id,
        CaptureSessionId = p.CaptureSessionId,
        Width = p.Width,
        Height = p.Height,
        ByteSize = p.ByteSize,
        Sha256 = p.Sha256,
        DownloadUrl = $"/v1/api/users/face-photos/{p.Id}/download",
        CreatedAt = p.CreatedAt,
    };

    private static string SafeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "face.jpg";
        var fileName = Path.GetFileName(name);
        return fileName.Length > 200 ? fileName[^200..] : fileName;
    }

    /// <summary>Auth0 subs contain '|' (e.g. <c>auth0|abc</c>, <c>google-oauth2|123</c>) — keep only key-safe chars
    /// so the R2 key is clean and prefix-listable for a future "delete all my data" sweep.</summary>
    private static string SanitizeIdentity(string identityId) =>
        new(identityId.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());

    /// <summary>Best-effort JPEG dimension read from the Start-Of-Frame marker (no image library). Returns (0,0) when
    /// it can't parse — dimensions are informational metadata only, never load-bearing.</summary>
    private static (int Width, int Height) ReadJpegDimensions(byte[] data)
    {
        var i = 2; // skip SOI (FF D8)
        while (i + 9 < data.Length)
        {
            if (data[i] != 0xFF) { i++; continue; }
            var marker = data[i + 1];
            // Standalone markers (no length payload): TEM (01), RSTn (D0–D7), SOI (D8), EOI (D9).
            if (marker is 0x01 or 0xD8 or 0xD9 || (marker >= 0xD0 && marker <= 0xD7)) { i += 2; continue; }
            if (i + 3 >= data.Length) break;
            var len = (data[i + 2] << 8) | data[i + 3];
            // SOFn markers carry size: C0–CF except C4 (DHT), C8 (JPG), CC (DAC).
            if (marker is >= 0xC0 and <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
            {
                if (i + 8 < data.Length)
                {
                    var height = (data[i + 5] << 8) | data[i + 6];
                    var width = (data[i + 7] << 8) | data[i + 8];
                    return (width, height);
                }
                break;
            }
            i += 2 + len;
        }
        return (0, 0);
    }

    private readonly record struct PreparedPhoto(byte[] Bytes, string Sha256, int Width, int Height, string FileName);
}
