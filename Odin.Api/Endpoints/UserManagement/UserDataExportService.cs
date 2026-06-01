using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Endpoints.UserManagement.Models;

namespace Odin.Api.Endpoints.UserManagement;

public interface IUserDataExportService
{
    /// <summary>
    /// Gathers everything the API stores about <paramref name="identityId"/>'s account into a
    /// single JSON-serializable bundle, intended for GDPR Article 20 portability requests.
    /// Returns null if no <c>application_users</c> row exists for the identity.
    /// </summary>
    Task<ExportMyDataContract.Response?> ExportAsync(string identityId, CancellationToken cancellationToken = default);
}

public class UserDataExportService(ApplicationDbContext dbContext) : IUserDataExportService
{
    public async Task<ExportMyDataContract.Response?> ExportAsync(
        string identityId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdentityId == identityId, cancellationToken);

        if (user is null) return null;

        // Orders are owned by IdentityId (CreatedBy), inspections by UserId (FK).
        // Both filters are needed because pre-provisioning orders existed with IdentityId only.
        var qpadmOrders = await dbContext.QpadmOrders
            .AsNoTracking()
            .Where(o => o.CreatedBy == identityId)
            .Include(o => o.GeneticInspection)
                .ThenInclude(gi => gi!.GeneticInspectionRegions)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        var g25Orders = await dbContext.G25Orders
            .AsNoTracking()
            .Where(o => o.CreatedBy == identityId)
            .Include(o => o.GeneticInspection)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        var rawFiles = await dbContext.RawGeneticFiles
            .AsNoTracking()
            .Where(f => f.CreatedBy == identityId && !f.IsDeleted)
            .Select(f => new ExportMyDataContract.RawGeneticFileSection
            {
                Id = f.Id,
                FileName = f.RawDataFileName,
                FileSize = f.RawData.Length,
                HasMergedData = f.MergedRawData != null && f.MergedRawData.Length > 0,
                MergedDataFileName = f.MergedRawDataFileName,
                CreatedAt = f.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var reports = await dbContext.Reports
            .AsNoTracking()
            .Where(r => r.UserId == user.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ExportMyDataContract.ReportSection
            {
                Id = r.Id,
                Type = r.Type.ToString(),
                Subject = r.Subject,
                Description = r.Description,
                Status = r.Status.ToString(),
                PageUrl = r.PageUrl,
                AttachmentFileName = r.FileName,
                CreatedAt = r.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var notifications = await dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == user.Id)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new ExportMyDataContract.NotificationSection
            {
                Id = n.Id,
                Type = n.Type.ToString(),
                Title = n.Title,
                Message = n.Message,
                IsRead = n.IsRead,
                ReadAt = n.ReadAt,
                CreatedAt = n.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var savedCoordinates = await dbContext.G25SavedCoordinates
            .AsNoTracking()
            .Where(c => c.UserId == user.Id)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new ExportMyDataContract.SavedCoordinateSection
            {
                Id = c.Id,
                Title = c.Title,
                RawInput = c.RawInput,
                Scaling = c.Scaling,
                AddMode = c.AddMode,
                CustomName = c.CustomName,
                ViewId = c.ViewId,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return new ExportMyDataContract.Response
        {
            ExportedAt = DateTime.UtcNow,
            SchemaVersion = "1",
            Profile = new ExportMyDataContract.ProfileSection
            {
                Id = user.Id,
                IdentityId = user.IdentityId,
                Email = user.Email,
                Username = user.Username,
                FirstName = user.FirstName,
                MiddleName = user.MiddleName,
                LastName = user.LastName,
                Role = user.Role.ToString(),
                Country = user.Country,
                CountryCode = user.CountryCode,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
            },
            QpadmOrders = qpadmOrders.Select(o => new ExportMyDataContract.QpadmOrderSection
            {
                Id = o.Id,
                Price = o.Price,
                Status = o.Status.ToString(),
                HasViewedResults = o.HasViewedResults,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt,
                GeneticInspection = o.GeneticInspection is null
                    ? null
                    : new ExportMyDataContract.QpadmInspectionSection
                    {
                        Id = o.GeneticInspection.Id,
                        FirstName = o.GeneticInspection.FirstName,
                        MiddleName = o.GeneticInspection.MiddleName,
                        LastName = o.GeneticInspection.LastName,
                        Gender = o.GeneticInspection.Gender?.ToString(),
                        HasProfilePicture = o.GeneticInspection.ProfilePicture is { Length: > 0 },
                        RawGeneticFileId = o.GeneticInspection.RawGeneticFileId,
                        RegionIds = o.GeneticInspection.GeneticInspectionRegions
                            .Select(gir => gir.RegionId).ToList(),
                    },
            }).ToList(),
            G25Orders = g25Orders.Select(o => new ExportMyDataContract.G25OrderSection
            {
                Id = o.Id,
                Price = o.Price,
                Status = o.Status.ToString(),
                HasViewedResults = o.HasViewedResults,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt,
                GeneticInspection = o.GeneticInspection is null
                    ? null
                    : new ExportMyDataContract.G25InspectionSection
                    {
                        Id = o.GeneticInspection.Id,
                        FirstName = o.GeneticInspection.FirstName,
                        MiddleName = o.GeneticInspection.MiddleName,
                        LastName = o.GeneticInspection.LastName,
                        Gender = o.GeneticInspection.Gender?.ToString(),
                        HasProfilePicture = o.GeneticInspection.ProfilePicture is { Length: > 0 },
                        RawGeneticFileId = o.GeneticInspection.RawGeneticFileId,
                        G25Coordinates = o.GeneticInspection.G25Coordinates,
                    },
            }).ToList(),
            RawGeneticFiles = rawFiles,
            Reports = reports,
            Notifications = notifications,
            SavedCoordinates = savedCoordinates,
        };
    }
}
