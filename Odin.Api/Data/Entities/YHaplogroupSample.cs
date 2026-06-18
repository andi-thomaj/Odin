using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    /// <summary>
    /// One geolocated, dated individual (from the AADR annotation) resolved to a YFull clade, imported
    /// from odin-tools-api to drive the haplogroup heatmap. <b>Shared reference data</b> (NOT
    /// <see cref="IAppScoped"/>). <see cref="Layer"/> is <c>ancient</c> or <c>modern</c> (present-day
    /// reference individual); <see cref="Era"/> is the coarse time-slider bucket. <see cref="TreeNodeId"/>
    /// references <see cref="YHaplogroupTreeNode.Id"/> (no DB-level FK: both tables are reload-replaced
    /// atomically per import, and the extractor guarantees the node exists). Natural key = AADR Genetic ID.
    /// </summary>
    public class YHaplogroupSample
    {
        public string GeneticId { get; set; } = string.Empty;
        public string IndividualId { get; set; } = string.Empty;

        /// <summary>Resolved YFull node id (see <see cref="YHaplogroupTreeNode.Id"/>).</summary>
        public string TreeNodeId { get; set; } = string.Empty;

        public string? YTerminal { get; set; }
        public string? YIsogg { get; set; }
        public string? YManual { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        /// <summary>Date mean in years before present (0 ≈ present-day reference individual).</summary>
        public double? DateMeanBp { get; set; }
        public double? DateSdBp { get; set; }
        public string? FullDate { get; set; }

        /// <summary>Coarse era bucket: Modern/Medieval/IronAge/BronzeAge/Neolithic/Mesolithic/Paleolithic/Unknown.</summary>
        public string Era { get; set; } = string.Empty;

        /// <summary><c>ancient</c> or <c>modern</c>.</summary>
        public string Layer { get; set; } = string.Empty;

        public string? Country { get; set; }
        public string? Locality { get; set; }
        public string? GroupId { get; set; }
        public string? Sex { get; set; }
        public string? Assessment { get; set; }

        public string DatasetVersion { get; set; } = string.Empty;

        /// <summary>Provenance: <c>AADR</c>, <c>HGDP-Bergstrom2020</c> (deep-Y backfill), or
        /// <c>Rrenjet</c> (Albanian DNA Project, used with permission). Lets enrichment rows be
        /// counted or removed independently.</summary>
        public string Source { get; set; } = "AADR";
    }

    public class YHaplogroupSampleConfiguration : IEntityTypeConfiguration<YHaplogroupSample>
    {
        public void Configure(EntityTypeBuilder<YHaplogroupSample> builder)
        {
            builder.HasKey(e => e.GeneticId);
            builder.Property(e => e.GeneticId).HasMaxLength(200);
            builder.Property(e => e.IndividualId).IsRequired().HasMaxLength(200);
            builder.Property(e => e.TreeNodeId).IsRequired().HasMaxLength(120);
            builder.Property(e => e.YTerminal).HasMaxLength(120);
            builder.Property(e => e.YIsogg).HasMaxLength(120);
            builder.Property(e => e.YManual).HasMaxLength(120);
            builder.Property(e => e.FullDate).HasMaxLength(200);
            builder.Property(e => e.Era).IsRequired().HasMaxLength(30);
            builder.Property(e => e.Layer).IsRequired().HasMaxLength(20);
            builder.Property(e => e.Country).HasMaxLength(200);
            builder.Property(e => e.Locality).HasMaxLength(500);
            builder.Property(e => e.GroupId).HasMaxLength(300);
            builder.Property(e => e.Sex).HasMaxLength(20);
            builder.Property(e => e.Assessment).HasMaxLength(50);
            builder.Property(e => e.DatasetVersion).IsRequired().HasMaxLength(50);
            builder.Property(e => e.Source).IsRequired().HasMaxLength(50).HasDefaultValue("AADR");

            // The heatmap joins samples to a clade's subtree by node id, then filters/groups by layer+era.
            builder.HasIndex(e => e.TreeNodeId);
            builder.HasIndex(e => e.Layer);

            builder.ToTable("y_haplogroup_samples");
        }
    }
}
