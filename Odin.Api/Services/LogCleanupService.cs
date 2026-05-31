using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;

namespace Odin.Api.Services
{
    public interface ILogCleanupService
    {
        Task<int> DeleteAllLogsAsync(CancellationToken cancellationToken = default);
    }

    public class LogCleanupService(ApplicationDbContext db, ILogger<LogCleanupService> logger) : ILogCleanupService
    {
        public async Task<int> DeleteAllLogsAsync(CancellationToken cancellationToken = default)
        {
            var deleted = await db.Logs.ExecuteDeleteAsync(cancellationToken);
            logger.LogInformation("LogCleanup: deleted {RowCount} rows from logs table", deleted);
            return deleted;
        }
    }
}
