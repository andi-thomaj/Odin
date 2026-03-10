using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.NotificationManagement.Models;
using Odin.Api.Hubs;

namespace Odin.Api.Endpoints.NotificationManagement
{
    public interface INotificationService
    {
        Task CreateAndSendAsync(int recipientUserId, NotificationType type, string title, string message,
            string? referenceId = null);

        Task<List<GetNotificationContract.Response>> GetNotificationsAsync(int userId, int page, int pageSize);
        Task<int> GetUnreadCountAsync(int userId);
        Task MarkAllAsReadAsync(int userId);
    }

    public class NotificationService(
        ApplicationDbContext dbContext,
        IHubContext<NotificationHub> hubContext) : INotificationService
    {
        public async Task CreateAndSendAsync(int recipientUserId, NotificationType type, string title,
            string message, string? referenceId = null)
        {
            var notification = new Notification
            {
                RecipientUserId = recipientUserId,
                Type = type,
                Title = title,
                Message = message,
                ReferenceId = referenceId,
                CreatedBy = string.Empty
            };

            dbContext.Notifications.Add(notification);
            await dbContext.SaveChangesAsync();

            var recipientUser = await dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == recipientUserId);

            if (recipientUser is null) return;

            var dto = new GetNotificationContract.Response
            {
                Id = notification.Id,
                Type = notification.Type.ToString(),
                Title = notification.Title,
                Message = notification.Message,
                IsRead = notification.IsRead,
                ReadAt = notification.ReadAt,
                ReferenceId = notification.ReferenceId,
                CreatedAt = notification.CreatedAt
            };

            await hubContext.Clients.User(recipientUser.IdentityId)
                .SendAsync("ReceiveNotification", dto);
        }

        public async Task<List<GetNotificationContract.Response>> GetNotificationsAsync(int userId, int page,
            int pageSize)
        {
            return await dbContext.Notifications
                .AsNoTracking()
                .Where(n => n.RecipientUserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new GetNotificationContract.Response
                {
                    Id = n.Id,
                    Type = n.Type.ToString(),
                    Title = n.Title,
                    Message = n.Message,
                    IsRead = n.IsRead,
                    ReadAt = n.ReadAt,
                    ReferenceId = n.ReferenceId,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await dbContext.Notifications
                .CountAsync(n => n.RecipientUserId == userId && !n.IsRead);
        }

        public async Task MarkAllAsReadAsync(int userId)
        {
            var now = DateTime.UtcNow;

            await dbContext.Notifications
                .Where(n => n.RecipientUserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, now));
        }
    }
}
