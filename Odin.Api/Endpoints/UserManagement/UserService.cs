using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.UserManagement.Models;

namespace Odin.Api.Endpoints.UserManagement
{
    public interface IUserService
    {
        Task<CreateUserContract.Response> CreateUserAsync(CreateUserContract.Request request);
        Task<GetUserContract.Response?> GetUserByIdentityIdAsync(string identityId);
        Task<UpdateUserContract.Response?> UpdateUserAsync(string identityId, UpdateUserContract.Request request);
        Task<UpdateUserRoleContract.Response?> UpdateUserRoleAsync(string identityId, UpdateUserRoleContract.Request request);
        Task<bool> DeleteUserAsync(string identityId);
    }

    public class UserService(ApplicationDbContext dbContext) : IUserService
    {
        public async Task<CreateUserContract.Response> CreateUserAsync(CreateUserContract.Request request)
        {
            var existingUser = await dbContext.Users
                .FirstOrDefaultAsync(u => u.IdentityId == request.IdentityId);

            if (existingUser is not null)
            {
                return new CreateUserContract.Response
                {
                    Id = existingUser.Id,
                    IdentityId = existingUser.IdentityId,
                    FirstName = existingUser.FirstName,
                    LastName = existingUser.LastName,
                    Email = existingUser.Email,
                    Role = existingUser.Role.ToString(),
                    IsNewUser = false
                };
            }

            var user = new User
            {
                IdentityId = request.IdentityId,
                Username = request.Username ?? request.Email,
                Email = request.Email,
                FirstName = request.FirstName ?? string.Empty,
                LastName = request.LastName ?? string.Empty,
                Role = AppRole.User,
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
                IsNewUser = true
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
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<UpdateUserContract.Response?> UpdateUserAsync(string identityId, UpdateUserContract.Request request)
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

            dbContext.Users.Remove(user);
            await dbContext.SaveChangesAsync();

            return true;
        }

        public async Task<UpdateUserRoleContract.Response?> UpdateUserRoleAsync(string identityId, UpdateUserRoleContract.Request request)
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
                Id = user.Id,
                IdentityId = user.IdentityId,
                Email = user.Email,
                Role = user.Role.ToString()
            };
        }
    }
}
