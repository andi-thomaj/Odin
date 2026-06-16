using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odin.Api.Data.Enums;

namespace Odin.Api.Data.Entities
{
    /// <summary>
    /// Cached Y-DNA clade-finder outcome for a qpAdm genetic inspection. Computed once (in a Hangfire
    /// background job) when the order is placed and read back on the result view — never recomputed per
    /// view. 1:1 with <see cref="QpadmGeneticInspection"/>. Mirrors the
    /// <c>AnalyzeCladeContract.Response</c> payload plus a <see cref="Status"/>/<see cref="Message"/> that
    /// tells the UI why a clade is or isn't available.
    /// </summary>
    public class QpadmCladeResult : BaseEntity, IAppScoped
    {
        public int Id { get; set; }
        /// <summary>Owning application (applications.key). Set from the parent inspection when computed in a
        /// background job; query-filtered — see <see cref="IAppScoped"/>.</summary>
        public string App { get; set; } = string.Empty;
        public int GeneticInspectionId { get; set; }
        public QpadmGeneticInspection GeneticInspection { get; set; } = null!;

        public CladeAnalysisStatus Status { get; set; }

        /// <summary>Human-readable reason shown to the user when <see cref="Status"/> is not Completed.</summary>
        public string? Message { get; set; }

        /// <summary>Schema/run marker for future re-analysis tracking (e.g. "v1").</summary>
        public string ResultsVersion { get; set; } = string.Empty;

        // ── Clade payload (only meaningful when Status == Completed) ──────────────
        public string? Clade { get; set; }
        public double? Score { get; set; }
        public string? NextPredictionClade { get; set; }
        public double? NextPredictionScore { get; set; }

        /// <summary>Paternal haplogroup lineage, backbone-most → terminal. Stored as a JSON array.</summary>
        public List<string> Lineage { get; set; } = [];

        /// <summary>Immediate sub-clades below the predicted clade. Stored as a JSON column (owned).</summary>
        public List<CladeDownstreamItem> Downstream { get; set; } = [];

        public int PositivesUsed { get; set; }
        public int NegativesUsed { get; set; }
        public int? YReads { get; set; }
        public string? SourceFormat { get; set; }
        public string? EffectiveBuild { get; set; }

        /// <summary>Non-fatal note from the analyzer (e.g. conflicting SNP calls).</summary>
        public string? Warning { get; set; }

        /// <summary>Set when a clade could not be determined from otherwise-valid Y data.</summary>
        public string? Error { get; set; }
    }

    /// <summary>One immediate sub-clade below the predicted clade (owned, JSON-serialized).</summary>
    public class CladeDownstreamItem
    {
        public string Clade { get; set; } = string.Empty;
        public int? Children { get; set; }
    }

    public class QpadmCladeResultConfiguration : IEntityTypeConfiguration<QpadmCladeResult>
    {
        public void Configure(EntityTypeBuilder<QpadmCladeResult> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(30);
            builder.Property(e => e.Message).HasMaxLength(2000);
            builder.Property(e => e.ResultsVersion).IsRequired().HasMaxLength(50);
            builder.Property(e => e.Clade).HasMaxLength(200);
            builder.Property(e => e.NextPredictionClade).HasMaxLength(200);
            builder.Property(e => e.SourceFormat).HasMaxLength(50);
            builder.Property(e => e.EffectiveBuild).HasMaxLength(20);
            builder.Property(e => e.Warning).HasMaxLength(2000);
            builder.Property(e => e.Error).HasMaxLength(2000);

            // Primitive collection — Npgsql maps List<string> to a JSON array column.
            builder.PrimitiveCollection(e => e.Lineage);

            // Owned collection serialized to a single JSON column (same pattern as G25AdmixtureResult.Ancestors).
            builder.OwnsMany(e => e.Downstream, b =>
            {
                b.ToJson();
                b.Property(d => d.Clade).IsRequired().HasMaxLength(200);
            });

            builder.HasOne(e => e.GeneticInspection)
                .WithOne(gi => gi.QpadmCladeResult)
                .HasForeignKey<QpadmCladeResult>(e => e.GeneticInspectionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => e.GeneticInspectionId).IsUnique();

            builder.Property(e => e.App).IsRequired().HasMaxLength(50);

            builder.ToTable("qpadm_clade_results");
        }
    }
}
