using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class ResearchLink
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public List<QpadmResultResearchLink> QpadmResultResearchLinks { get; set; } = [];
    }

    public class ResearchLinkConfiguration : IEntityTypeConfiguration<ResearchLink>
    {
        public void Configure(EntityTypeBuilder<ResearchLink> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Title).IsRequired().HasMaxLength(200);
            builder.Property(e => e.Url).IsRequired().HasMaxLength(500);

            builder.ToTable("research_links");
        }
    }
}
