using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odin.Api.Data.Enums;

namespace Odin.Api.Data.Entities
{
    /// <summary>
    /// Provenance record for one run of the rerunnable Y-haplogroup heatmap import (AADR + YFull →
    /// <see cref="YHaplogroupSample"/>/<see cref="YHaplogroupTreeNode"/>). Each run replaces the
    /// reference rows wholesale, so this row is the audit trail: when it ran, the source dataset
    /// version, how many rows landed, and any failure. The latest <see cref="HaplogroupImportStatus.Completed"/>
    /// run also acts as the cache-busting token for the distribution endpoint. Shared reference data.
    /// </summary>
    public class HaplogroupImportRun
    {
        public int Id { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public HaplogroupImportStatus Status { get; set; }

        /// <summary>Source release imported (e.g. AADR <c>v66.p1_HO</c>); null until the export meta is read.</summary>
        public string? DatasetVersion { get; set; }

        public int SampleCount { get; set; }
        public int NodeCount { get; set; }

        /// <summary>Individuals with a Y-haplogroup that did not resolve to any tree node (diagnostic).</summary>
        public int UnresolvedCount { get; set; }

        public string? Error { get; set; }

        /// <summary>Who/what triggered the run — an admin user id, or "system" for the CLI/headless path.</summary>
        public string TriggeredBy { get; set; } = string.Empty;
    }

    public class HaplogroupImportRunConfiguration : IEntityTypeConfiguration<HaplogroupImportRun>
    {
        public void Configure(EntityTypeBuilder<HaplogroupImportRun> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
            builder.Property(e => e.DatasetVersion).HasMaxLength(50);
            builder.Property(e => e.Error).HasMaxLength(4000);
            builder.Property(e => e.TriggeredBy).IsRequired().HasMaxLength(200);

            // The status endpoint reads the most recent run; the cache token reads the latest Completed one.
            builder.HasIndex(e => e.StartedAt);

            builder.ToTable("haplogroup_import_runs");
        }
    }
}
