using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    /// <summary>
    /// Present-day Y-haplogroup frequency by country — the aggregated "frequency layer" that
    /// complements the per-individual sample bubbles. One row = <c>% of a clade in a country</c>
    /// (n-weighted across studies), keyed by the clade's YFull node id so a user's clade can be
    /// ancestor-matched to its most-specific available frequency. Built from Wikipedia's
    /// "Y-DNA haplogroups in populations of …" tables (<b>CC BY-SA 4.0</b> — attribution + share-alike).
    /// <b>Shared reference data, a different grain from <see cref="YHaplogroupSample"/></b> (frequencies,
    /// not genotypes), so it cannot duplicate individual samples; it never joins to them.
    /// </summary>
    public class ModernHaplogroupFrequency
    {
        public int Id { get; set; }
        public string Country { get; set; } = string.Empty;

        /// <summary>Highcharts world-map join key (ISO-3166-1 alpha-2, lowercase, e.g. <c>al</c>).</summary>
        public string HcKey { get; set; } = string.Empty;

        /// <summary>YFull node the % refers to (the resolved Wikipedia column clade, e.g. <c>J</c>, <c>R1b</c>).</summary>
        public string CladeNodeId { get; set; } = string.Empty;

        public double Percentage { get; set; }
        public int SampleSize { get; set; }
        public int StudyCount { get; set; }

        public string License { get; set; } = "CC BY-SA 4.0";
    }

    public class ModernHaplogroupFrequencyConfiguration : IEntityTypeConfiguration<ModernHaplogroupFrequency>
    {
        public void Configure(EntityTypeBuilder<ModernHaplogroupFrequency> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Country).IsRequired().HasMaxLength(120);
            builder.Property(e => e.HcKey).IsRequired().HasMaxLength(10);
            builder.Property(e => e.CladeNodeId).IsRequired().HasMaxLength(120);
            builder.Property(e => e.License).IsRequired().HasMaxLength(50);

            // The distribution query ancestor-matches the user's clade to a CladeNodeId.
            builder.HasIndex(e => e.CladeNodeId);

            builder.ToTable("modern_haplogroup_frequencies");
        }
    }
}
