using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.UserManagement.Models;
using Odin.Api.Services;

namespace Odin.Api.Endpoints.UserManagement
{
    public interface IUserService
    {
        Task<CreateUserContract.Response> CreateUserAsync(CreateUserContract.Request request, string? ipAddress = null);
        Task<GetUserContract.Response?> GetUserByIdentityIdAsync(string identityId);
        Task<UpdateUserContract.Response?> UpdateUserAsync(string identityId, UpdateUserContract.Request request);

        Task<UpdateUserRoleContract.Response?> UpdateUserRoleAsync(string identityId,
            UpdateUserRoleContract.Request request);

        Task<bool> DeleteUserAsync(string identityId);

        Task<ListUsersContract.Response> ListUsersAsync(int skip, int take);
    }

    public class UserService(
        ApplicationDbContext dbContext,
        IGeoLocationService geoLocationService) : IUserService
    {
        public async Task<CreateUserContract.Response> CreateUserAsync(CreateUserContract.Request request,
            string? ipAddress = null)
        {
            var existingUser = await dbContext.Users
                .FirstOrDefaultAsync(u => u.IdentityId == request.IdentityId);

            if (existingUser is not null)
            {
                if ((existingUser.Country is null || existingUser.CountryCode is null) && ipAddress is not null)
                {
                    var geo = await geoLocationService.GetCountryFromIpAsync(ipAddress);
                    existingUser.Country = geo?.Country;
                    existingUser.CountryCode = geo?.CountryCode;
                    await dbContext.SaveChangesAsync();
                }

                return new CreateUserContract.Response
                {
                    Id = existingUser.Id,
                    IdentityId = existingUser.IdentityId,
                    FirstName = existingUser.FirstName,
                    LastName = existingUser.LastName,
                    Email = existingUser.Email,
                    Role = existingUser.Role.ToString(),
                    IsNewUser = false,
                };
            }

            var geoResult = await geoLocationService.GetCountryFromIpAsync(ipAddress);

            var user = new User
            {
                IdentityId = request.IdentityId,
                Username = request.Username ?? request.Email,
                Email = request.Email,
                FirstName = request.FirstName ?? string.Empty,
                MiddleName = string.IsNullOrWhiteSpace(request.MiddleName) ? string.Empty : request.MiddleName.Trim(),
                LastName = request.LastName ?? string.Empty,
                Role = AppRole.User,
                Country = geoResult?.Country,
                CountryCode = geoResult?.CountryCode,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = request.IdentityId,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = request.IdentityId
            };

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            return new CreateUserContract.Response
            {
                Id = user.Id,
                IdentityId = user.IdentityId,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role.ToString(),
                IsNewUser = true,
            };
        }

        public async Task<GetUserContract.Response?> GetUserByIdentityIdAsync(string identityId)
        {
            var user = await dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.IdentityId == identityId);

            if (user is null) return null;

            return new GetUserContract.Response
            {
                Id = user.Id,
                IdentityId = user.IdentityId,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                MiddleName = user.MiddleName,
                LastName = user.LastName,
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt,
            };
        }

        public async Task<UpdateUserContract.Response?> UpdateUserAsync(string identityId,
            UpdateUserContract.Request request)
        {
            var user = await dbContext.Users
                .FirstOrDefaultAsync(u => u.IdentityId == identityId);

            if (user is null) return null;

            user.FirstName = request.FirstName ?? user.FirstName;
            user.MiddleName = request.MiddleName ?? user.MiddleName;
            user.LastName = request.LastName ?? user.LastName;
            user.Username = request.Username ?? user.Username;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = identityId;

            await dbContext.SaveChangesAsync();

            return new UpdateUserContract.Response
            {
                Id = user.Id,
                IdentityId = user.IdentityId,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                MiddleName = user.MiddleName,
                LastName = user.LastName,
                Role = user.Role.ToString()
            };
        }

        public async Task<bool> DeleteUserAsync(string identityId)
        {
            var user = await dbContext.Users
                .FirstOrDefaultAsync(u => u.IdentityId == identityId);

            if (user is null) return false;

            // Collect IDs of entities that won't cascade from User deletion.
            // Order and RawGeneticFile are principals (GeneticInspection holds the FK),
            // so deleting GeneticInspection leaves them orphaned.
            var inspectionData = await dbContext.QpadmGeneticInspections
                .Where(gi => gi.UserId == user.Id)
                .Select(gi => new { gi.OrderId, gi.RawGeneticFileId })
                .ToListAsync();

            var orderIds = inspectionData.Select(d => d.OrderId).ToList();
            var rawFileIds = inspectionData.Select(d => d.RawGeneticFileId).Distinct().ToList();

            await using var transaction = await dbContext.Database.BeginTransactionAsync();

            // 1. Delete user — DB cascades to: GeneticInspections (→ regions, QpadmResult chain),
            //    Notifications, Reports
            dbContext.Users.Remove(user);
            await dbContext.SaveChangesAsync();

            // 2. Delete orphaned Orders. Addons are stored as a JSON snapshot on the order row,
            //    so no cascade is needed for them.
            if (orderIds.Count > 0)
            {
                await dbContext.QpadmOrders
                    .Where(o => orderIds.Contains(o.Id))
                    .ExecuteDeleteAsync();
            }

            // 3. Delete orphaned RawGeneticFiles not referenced by other users' inspections
            if (rawFileIds.Count > 0)
            {
                await dbContext.RawGeneticFiles
                    .IgnoreQueryFilters()
                    .Where(rf => rawFileIds.Contains(rf.Id))
                    .Where(rf => !dbContext.QpadmGeneticInspections.Any(gi => gi.RawGeneticFileId == rf.Id))
                    .ExecuteDeleteAsync();
            }

            await transaction.CommitAsync();

            return true;
        }

        public async Task<UpdateUserRoleContract.Response?> UpdateUserRoleAsync(string identityId,
            UpdateUserRoleContract.Request request)
        {
            var user = await dbContext.Users
                .FirstOrDefaultAsync(u => u.IdentityId == identityId);

            if (user is null) return null;

            if (Enum.TryParse<AppRole>(request.Role, ignoreCase: true, out var parsedRole))
            {
                user.Role = parsedRole;
            }
            else
            {
                return null;
            }

            user.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();

            return new UpdateUserRoleContract.Response
            {
                Id = user.Id, IdentityId = user.IdentityId, Email = user.Email, Role = user.Role.ToString()
            };
        }

        public async Task<ListUsersContract.Response> ListUsersAsync(int skip, int take)
        {
            var query = dbContext.Users.AsNoTracking().OrderByDescending(u => u.CreatedAt);

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip(skip)
                .Take(take)
                .Select(u => new ListUsersContract.UserItem
                {
                    Id = u.Id,
                    IdentityId = u.IdentityId,
                    Username = u.Username,
                    Email = u.Email,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Role = u.Role.ToString(),
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            return new ListUsersContract.Response
            {
                Items = items,
                TotalCount = totalCount,
                Skip = skip,
                Take = take
            };
        }
    }
}
