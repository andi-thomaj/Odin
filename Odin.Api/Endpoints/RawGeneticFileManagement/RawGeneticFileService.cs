using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.RawGeneticFileManagement.Models;
using Odin.Api.Services;

namespace Odin.Api.Endpoints.RawGeneticFileManagement
{
    public interface IRawGeneticFileService
    {
        Task<UploadGeneticFileContract.Response> UploadFileAsync(UploadGeneticFileContract.Request request, string identityId);
        Task<GetGeneticFileContract.Response?> GetFileByIdAsync(int id, string identityId);
        Task<IEnumerable<GetGeneticFileContract.Response>> GetAllFilesAsync(string identityId);
        Task<(byte[]? Data, string? FileName, int StatusCode)> DownloadFileAsync(int id, string identityId);
        Task<(bool Deleted, int StatusCode)> DeleteFileAsync(int id, string identityId);
    }

    public class RawGeneticFileService(ApplicationDbContext dbContext) : IRawGeneticFileService
    {
        public async Task<UploadGeneticFileContract.Response> UploadFileAsync(UploadGeneticFileContract.Request request, string identityId)
        {
            const long maxFileSize = 50 * 1024 * 1024; // 50 MB
            if (request.File.Length > maxFileSize)
                throw new InvalidOperationException("Genetic file size must not exceed 50 MB.");

            using var memoryStream = new MemoryStream();
            await request.File.CopyToAsync(memoryStream);
            var data = memoryStream.ToArray();

            if (!FileSignatureValidator.LooksLikeGeneticFile(data))
                throw new InvalidOperationException(
                    "Uploaded file does not look like a valid genetic data file. " +
                    "Expected a vendor raw-data export (23andMe, AncestryDNA, MyHeritage, FTDNA, ...) as text, ZIP, or GZIP.");

            var rawGeneticFile = new RawGeneticFile
            {
                RawDataFileName = request.File.FileName, RawData = data, CreatedBy = identityId
            };

            dbContext.RawGeneticFiles.Add(rawGeneticFile);
            await dbContext.SaveChangesAsync();

            return new UploadGeneticFileContract.Response
            {
                Id = rawGeneticFile.Id,
                FileName = rawGeneticFile.RawDataFileName,
                FileSize = request.File.Length,
                UploadedAt = DateTime.UtcNow
            };
        }

        public async Task<GetGeneticFileContract.Response?> GetFileByIdAsync(int id, string identityId)
        {
            // Project in the DB (FileSize comes from a server-side length() on the blob column) so the
            // multi-MB RawData / Converted23AndMeData / MergedRawData blobs are never loaded just to
            // return metadata.
            return await dbContext.RawGeneticFiles
                .AsNoTracking()
                .Where(f => f.Id == id && f.CreatedBy == identityId && !f.IsDeleted)
                .Select(f => new GetGeneticFileContract.Response
                {
                    Id = f.Id, FileName = f.RawDataFileName, FileSize = f.RawData.Length
                })
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<GetGeneticFileContract.Response>> GetAllFilesAsync(string identityId)
        {
            return await dbContext.RawGeneticFiles
                .AsNoTracking()
                .Where(f => f.CreatedBy == identityId && !f.IsDeleted)
                .Select(f => new GetGeneticFileContract.Response
                {
                    Id = f.Id, FileName = f.RawDataFileName, FileSize = f.RawData.Length
                })
                .ToListAsync();
        }

        public async Task<(byte[]? Data, string? FileName, int StatusCode)> DownloadFileAsync(int id, string identityId)
        {
            // Only RawData is needed here; project it so the other blob columns (Converted23AndMeData,
            // MergedRawData) aren't dragged into memory. The global !IsDeleted filter still applies, so
            // a soft-deleted file returns null → 404.
            var file = await dbContext.RawGeneticFiles
                .AsNoTracking()
                .Where(f => f.Id == id)
                .Select(f => new { f.RawData, f.RawDataFileName, f.CreatedBy })
                .FirstOrDefaultAsync();

            if (file is null)
            {
                return (null, null, 404);
            }

            if (file.CreatedBy != identityId)
            {
                return (null, null, 403);
            }

            return (file.RawData, file.RawDataFileName, 200);
        }

        public async Task<(bool Deleted, int StatusCode)> DeleteFileAsync(int id, string identityId)
        {
            // Look up only the owner for the auth check — no need to materialize the (multi-MB) blobs
            // just to flip a flag. IgnoreQueryFilters so an already soft-deleted row is still found
            // (the delete stays idempotent, matching the previous FindAsync key-lookup semantics).
            var meta = await dbContext.RawGeneticFiles
                .IgnoreQueryFilters()
                .Where(f => f.Id == id)
                .Select(f => new { f.CreatedBy })
                .FirstOrDefaultAsync();

            if (meta is null)
            {
                return (false, 404);
            }

            if (meta.CreatedBy != identityId)
            {
                return (false, 403);
            }

            var inUse = await dbContext.QpadmGeneticInspections
                .AnyAsync(gi => gi.RawGeneticFileId == id
                    && (gi.Order.Status == OrderStatus.Pending || gi.Order.Status == OrderStatus.InProcess));

            if (inUse)
                throw new InvalidOperationException(
                    "This file cannot be deleted because it is used by an order that is still Pending or In Process.");

            // Set the flag with a direct UPDATE so the blobs are never read or rewritten.
            await dbContext.RawGeneticFiles
                .IgnoreQueryFilters()
                .Where(f => f.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsDeleted, true));
            return (true, 200);
        }
    }
}
