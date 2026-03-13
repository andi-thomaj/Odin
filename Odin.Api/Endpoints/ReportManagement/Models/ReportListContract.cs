namespace Odin.Api.Endpoints.ReportManagement.Models
{
    public class ReportListContract
    {
        public class ListItem
        {
            public int Id { get; set; }
            public string Type { get; set; } = string.Empty;
            public string Subject { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string? UserName { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public class Detail
        {
            public int Id { get; set; }
            public string Type { get; set; } = string.Empty;
            public string Subject { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string? AdminNotes { get; set; }
            public string? PageUrl { get; set; }
            public string? FileName { get; set; }
            public bool HasFile { get; set; }
            public string UserName { get; set; } = string.Empty;
            public string UserEmail { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
        }
    }
}
