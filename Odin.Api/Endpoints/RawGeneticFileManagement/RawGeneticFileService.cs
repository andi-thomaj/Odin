using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.RawGeneticFileManagement.Models;

namespace Odin.Api.Endpoints.RawGeneticFileManagement
{
    public interface IRawGeneticFileService
    {
        Task<UploadGeneticFileContract.Response> UploadFileAsync(UploadGeneticFileContract.Request request);
        Task<GetGeneticFileContract.Response?> GetFileByIdAsync(int id);
        Task<IEnumerable<GetGeneticFileContract.Response>> GetAllFilesAsync();
        Task<(byte[] Data, string FileName)?> DownloadFileAsync(int id);
        Task<bool> DeleteFileAsync(int id);
    }

    public class RawGeneticFileService(ApplicationDbContext dbContext) : IRawGeneticFileService
    {
        public async Task<UploadGeneticFileContract.Response> UploadFileAsync(UploadGeneticFileContract.Request request)
        {
            using var memoryStream = new MemoryStream();
            await request.File.CopyToAsync(memoryStream);

            var rawGeneticFile = new RawGeneticFile
            {
                FileName = request.File.FileName,
                RawData = memoryStream.ToArray()
            };

            dbContext.RawGeneticFiles.Add(rawGeneticFile);
            await dbContext.SaveChangesAsync();

            return new UploadGeneticFileContract.Response
            {
                Id = rawGeneticFile.Id,
                FileName = rawGeneticFile.FileName,
                FileSize = request.File.Length,
                UploadedAt = DateTime.UtcNow
            };
        }

        public async Task<GetGeneticFileContract.Response?> GetFileByIdAsync(int id)
        {
            var file = await dbContext.RawGeneticFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == id);

            if (file is null)
            {
                return null;
            }

            return new GetGeneticFileContract.Response
            {
                Id = file.Id,
                FileName = file.FileName,
                FileSize = file.RawData.Length
            };
        }

        public async Task<IEnumerable<GetGeneticFileContract.Response>> GetAllFilesAsync()
        {
            return await dbContext.RawGeneticFiles
                .AsNoTracking()
                .Select(f => new GetGeneticFileContract.Response
                {
                    Id = f.Id,
                    FileName = f.FileName,
                    FileSize = f.RawData.Length
                })
                .ToListAsync();
        }

        public async Task<(byte[] Data, string FileName)?> DownloadFileAsync(int id)
        {
            var file = await dbContext.RawGeneticFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == id);

            if (file is null)
            {
                return null;
            }

            return (file.RawData, file.FileName);
        }

        public async Task<bool> DeleteFileAsync(int id)
        {
            var file = await dbContext.RawGeneticFiles.FindAsync(id);

            if (file is null)
            {
                return false;
            }

            dbContext.RawGeneticFiles.Remove(file);
            await dbContext.SaveChangesAsync();
            return true;
        }
    }
}
