namespace Odin.Api.Data.Entities
{
    public class BaseEntity
    {
        public DateTime CreatedAt { get; set; }
        public required string CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;
    }
}
