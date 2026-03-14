using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;

namespace Odin.Api.Endpoints.NotificationManagement
{
    public static class NotificationEndpoints
    {
        public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
        {
            var endpoints = app.MapGroup("api/notifications");

            endpoints.MapGet("/", GetAll).RequireAuthorization("Authenticated");
            endpoints.MapGet("/unread-count", GetUnreadCount).RequireAuthorization("Authenticated");
            endpoints.MapPatch("/read-status", MarkAllAsRead).RequireAuthorization("Authenticated");
        }

        private static async Task<IResult> GetAll(
            INotificationService service,
            ApplicationDbContext dbContext,
            HttpContext httpContext,
            int page = 1,
            int pageSize = 20)
        {
            var userId = await ResolveUserId(httpContext, dbContext);
            if (userId is null) return Results.Unauthorized();

            var notifications = await service.GetNotificationsAsync(userId.Value, page, pageSize);
            return Results.Ok(notifications);
        }

        private static async Task<IResult> GetUnreadCount(
            INotificationService service,
            ApplicationDbContext dbContext,
            HttpContext httpContext)
        {
            var userId = await ResolveUserId(httpContext, dbContext);
            if (userId is null) return Results.Unauthorized();

            var count = await service.GetUnreadCountAsync(userId.Value);
            return Results.Ok(new { count });
        }

        private static async Task<IResult> MarkAllAsRead(
            INotificationService service,
            ApplicationDbContext dbContext,
            HttpContext httpContext)
        {
            var userId = await ResolveUserId(httpContext, dbContext);
            if (userId is null) return Results.Unauthorized();

            await service.MarkAllAsReadAsync(userId.Value);
            return Results.NoContent();
        }

        private static async Task<int?> ResolveUserId(HttpContext httpContext, ApplicationDbContext dbContext)
        {
            var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? httpContext.User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(identityId)) return null;

            var user = await dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.IdentityId == identityId);

            return user?.Id;
        }
    }
}
