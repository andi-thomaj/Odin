using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class QpadmResultResearchLink
    {
        public int QpadmResultId { get; set; }
        public required QpadmResult QpadmResult { get; set; }
        public int ResearchLinkId { get; set; }
        public required ResearchLink ResearchLink { get; set; }
    }

    public class QpadmResultResearchLinkConfiguration : IEntityTypeConfiguration<QpadmResultResearchLink>
    {
        public void Configure(EntityTypeBuilder<QpadmResultResearchLink> builder)
        {
            builder.HasKey(e => new { e.QpadmResultId, e.ResearchLinkId });
            builder.HasOne(e => e.QpadmResult)
                .WithMany(e => e.QpadmResultResearchLinks)
                .HasForeignKey(e => e.QpadmResultId);
            builder.HasOne(e => e.ResearchLink)
                .WithMany(e => e.QpadmResultResearchLinks)
                .HasForeignKey(e => e.ResearchLinkId);

            builder.ToTable("qpadm_result_research_links");
        }
    }
}
