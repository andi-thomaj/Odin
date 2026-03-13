using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.NotificationManagement;
using Odin.Api.Endpoints.ReportManagement.Models;

namespace Odin.Api.Endpoints.ReportManagement
{
    public interface IReportService
    {
        Task<CreateReportContract.Response> CreateAsync(int userId, CreateReportContract.Request request,
            IFormFile? file);

        Task<List<ReportListContract.ListItem>> GetUserReportsAsync(int userId, int page, int pageSize);

        Task<List<ReportListContract.ListItem>> GetAllReportsAsync(int page, int pageSize,
            ReportType? typeFilter, ReportStatus? statusFilter);

        Task<ReportListContract.Detail?> GetReportDetailAsync(int reportId, int? userId);
        Task<UpdateReportStatusContract.Response?> UpdateStatusAsync(int reportId, UpdateReportStatusContract.Request request);
        Task<(byte[] data, string contentType, string fileName)?> GetFileAsync(int reportId, int? userId);
    }

    public class ReportService(
        ApplicationDbContext dbContext,
        INotificationService notificationService) : IReportService
    {
        private static readonly HashSet<string> AllowedContentTypes =
        [
            "image/png", "image/jpeg", "image/gif", "application/pdf"
        ];

        private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

        public async Task<CreateReportContract.Response> CreateAsync(int userId,
            CreateReportContract.Request request, IFormFile? file)
        {
            var reportType = Enum.Parse<ReportType>(request.Type, ignoreCase: true);

            var report = new Report
            {
                UserId = userId,
                Type = reportType,
                Subject = request.Subject.Trim(),
                Description = request.Description.Trim(),
                Status = ReportStatus.Pending,
                PageUrl = request.PageUrl,
                CreatedBy = userId.ToString(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (file is { Length: > 0 })
            {
                if (file.Length > MaxFileSize)
                    throw new InvalidOperationException("File size must not exceed 5 MB.");

                if (!AllowedContentTypes.Contains(file.ContentType))
                    throw new InvalidOperationException("File type must be PNG, JPEG, GIF, or PDF.");

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                report.FileName = file.FileName;
                report.FileData = ms.ToArray();
                report.FileContentType = file.ContentType;
            }

            dbContext.Reports.Add(report);
            await dbContext.SaveChangesAsync();

            // Notify all admins
            var adminUsers = await dbContext.Users
                .AsNoTracking()
                .Where(u => u.Role == AppRole.Admin)
                .ToListAsync();

            foreach (var admin in adminUsers)
            {
                await notificationService.CreateAndSendAsync(
                    admin.Id,
                    NotificationType.NewReport,
                    "New Report Submitted",
                    $"A new {reportType} report was submitted: {report.Subject}",
                    report.Id.ToString());
            }

            return new CreateReportContract.Response
            {
                Id = report.Id,
                Type = report.Type.ToString(),
                Subject = report.Subject,
                Description = report.Description,
                Status = report.Status.ToString(),
                PageUrl = report.PageUrl,
                FileName = report.FileName,
                CreatedAt = report.CreatedAt
            };
        }

        public async Task<List<ReportListContract.ListItem>> GetUserReportsAsync(int userId, int page,
            int pageSize)
        {
            return await dbContext.Reports
                .AsNoTracking()
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new ReportListContract.ListItem
                {
                    Id = r.Id,
                    Type = r.Type.ToString(),
                    Subject = r.Subject,
                    Status = r.Status.ToString(),
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<List<ReportListContract.ListItem>> GetAllReportsAsync(int page, int pageSize,
            ReportType? typeFilter, ReportStatus? statusFilter)
        {
            var query = dbContext.Reports
                .AsNoTracking()
                .Include(r => r.User)
                .AsQueryable();

            if (typeFilter.HasValue)
                query = query.Where(r => r.Type == typeFilter.Value);

            if (statusFilter.HasValue)
                query = query.Where(r => r.Status == statusFilter.Value);

            return await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new ReportListContract.ListItem
                {
                    Id = r.Id,
                    Type = r.Type.ToString(),
                    Subject = r.Subject,
                    Status = r.Status.ToString(),
                    UserName = r.User.FirstName + " " + r.User.LastName,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<ReportListContract.Detail?> GetReportDetailAsync(int reportId, int? userId)
        {
            var query = dbContext.Reports
                .AsNoTracking()
                .Include(r => r.User)
                .Where(r => r.Id == reportId);

            if (userId.HasValue)
                query = query.Where(r => r.UserId == userId.Value);

            return await query
                .Select(r => new ReportListContract.Detail
                {
                    Id = r.Id,
                    Type = r.Type.ToString(),
                    Subject = r.Subject,
                    Description = r.Description,
                    Status = r.Status.ToString(),
                    AdminNotes = r.AdminNotes,
                    PageUrl = r.PageUrl,
                    FileName = r.FileName,
                    HasFile = r.FileData != null,
                    UserName = r.User.FirstName + " " + r.User.LastName,
                    UserEmail = r.User.Email,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                })
                .FirstOrDefaultAsync();
        }

        public async Task<UpdateReportStatusContract.Response?> UpdateStatusAsync(int reportId,
            UpdateReportStatusContract.Request request)
        {
            var report = await dbContext.Reports
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report is null) return null;

            var newStatus = Enum.Parse<ReportStatus>(request.Status, ignoreCase: true);
            report.Status = newStatus;
            report.AdminNotes = request.AdminNotes;
            report.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();

            // Notify the report owner about status change
            await notificationService.CreateAndSendAsync(
                report.UserId,
                NotificationType.ReportStatusUpdated,
                "Report Status Updated",
                $"Your report \"{report.Subject}\" has been updated to {newStatus}.",
                report.Id.ToString());

            return new UpdateReportStatusContract.Response
            {
                Id = report.Id,
                Type = report.Type.ToString(),
                Subject = report.Subject,
                Description = report.Description,
                Status = report.Status.ToString(),
                AdminNotes = report.AdminNotes,
                PageUrl = report.PageUrl,
                FileName = report.FileName,
                UserName = report.User.FirstName + " " + report.User.LastName,
                UserEmail = report.User.Email,
                CreatedAt = report.CreatedAt,
                UpdatedAt = report.UpdatedAt
            };
        }

        public async Task<(byte[] data, string contentType, string fileName)?> GetFileAsync(int reportId,
            int? userId)
        {
            var query = dbContext.Reports.Where(r => r.Id == reportId);

            if (userId.HasValue)
                query = query.Where(r => r.UserId == userId.Value);

            var report = await query
                .Select(r => new { r.FileData, r.FileContentType, r.FileName })
                .FirstOrDefaultAsync();

            if (report?.FileData is null || report.FileContentType is null || report.FileName is null)
                return null;

            return (report.FileData, report.FileContentType, report.FileName);
        }
    }
}
