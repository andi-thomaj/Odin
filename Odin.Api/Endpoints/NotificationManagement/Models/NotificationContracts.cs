namespace Odin.Api.Endpoints.NotificationManagement.Models
{
    public class GetNotificationContract
    {
        public class Response
        {
            public int Id { get; set; }
            public string Type { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public bool IsRead { get; set; }
            public DateTime? ReadAt { get; set; }
            public string? ReferenceId { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }

    public class UnreadCountContract
    {
        public class Response
        {
            public int Count { get; set; }
        }
    }
}
