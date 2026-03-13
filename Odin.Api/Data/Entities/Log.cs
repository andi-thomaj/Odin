using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class Log
    {
        public int Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? MessageTemplate { get; set; }
        public string Level { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? Exception { get; set; }
        public string? Properties { get; set; }
    }

    public class LogConfiguration : IEntityTypeConfiguration<Log>
    {
        public void Configure(EntityTypeBuilder<Log> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Message).IsRequired();
            builder.Property(e => e.Level).IsRequired().HasMaxLength(50);
            builder.Property(e => e.Timestamp).IsRequired();

            builder.HasIndex(e => e.Timestamp);

            builder.ToTable("logs");
        }
    }
}
