using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.RawGeneticFileManagement.Models;

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

            var rawGeneticFile = new RawGeneticFile
            {
                RawDataFileName = request.File.FileName, RawData = memoryStream.ToArray(), CreatedBy = identityId
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
            var file = await dbContext.RawGeneticFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == id && f.CreatedBy == identityId && !f.IsDeleted);

            if (file is null)
            {
                return null;
            }

            return new GetGeneticFileContract.Response
            {
                Id = file.Id, FileName = file.RawDataFileName, FileSize = file.RawData.Length
            };
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
            var file = await dbContext.RawGeneticFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == id);

            if (file is null || file.IsDeleted)
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
            var file = await dbContext.RawGeneticFiles.FindAsync(id);

            if (file is null)
            {
                return (false, 404);
            }

            if (file.CreatedBy != identityId)
            {
                return (false, 403);
            }

            var inUse = await dbContext.GeneticInspections
                .AnyAsync(gi => gi.RawGeneticFileId == id
                    && (gi.Order.Status == OrderStatus.Pending || gi.Order.Status == OrderStatus.InProcess));

            if (inUse)
                throw new InvalidOperationException(
                    "This file cannot be deleted because it is used by an order that is still Pending or In Process.");

            file.IsDeleted = true;
            await dbContext.SaveChangesAsync();
            return (true, 200);
        }
    }
}
