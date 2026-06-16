using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odin.Api.Data.Enums;

namespace Odin.Api.Data.Entities
{
    public class RawGeneticFile : BaseEntity, IAppScoped
    {
        public int Id { get; set; }
        /// <summary>Owning application (applications.key). Auto-stamped + query-filtered — see <see cref="IAppScoped"/>.</summary>
        public string App { get; set; } = string.Empty;
        public required byte[] RawData { get; set; } = [];
        public required string RawDataFileName { get; set; }

        // The user's raw upload normalized to 23andMe text (small) — produced by the automated
        // convert+merge job and kept in the DB. Feeds the AADR merge and any future re-runs.
        public byte[]? Converted23AndMeData { get; set; }
        public string? Converted23AndMeFileName { get; set; }

        // Legacy: merged dataset historically uploaded by hand as a DB blob. Superseded by the
        // filesystem-backed merge (MergeId below); retained for backward compatibility.
        public byte[]? MergedRawData { get; set; }
        public string? MergedRawDataFileName { get; set; }

        // Automated AADR merge tracking. The bundle bytes live on the odin-tools-api volume, not
        // in Postgres — only this metadata is stored here.
        public MergeStatus MergeStatus { get; set; } = MergeStatus.NotStarted;
        public string? MergeId { get; set; }
        public string? MergeFileName { get; set; }
        public long? MergeSizeBytes { get; set; }
        // Wall-clock the automated convert+merge took to produce the bundle (set on success). Null
        // until a merge completes; preserved after the bundle is deleted so the duration stays visible.
        public double? MergeDurationSeconds { get; set; }
        public string? MergeError { get; set; }

        public bool IsDeleted { get; set; }
        public List<QpadmGeneticInspection> GeneticInspections { get; set; } = [];
    }

    public class RawGeneticFileConfiguration : IEntityTypeConfiguration<RawGeneticFile>
    {
        public void Configure(EntityTypeBuilder<RawGeneticFile> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.RawDataFileName).IsRequired().HasMaxLength(200);
            builder.Property(e => e.Converted23AndMeFileName).HasMaxLength(200);
            builder.Property(e => e.MergedRawDataFileName).HasMaxLength(200);
            builder.Property(e => e.MergeStatus).IsRequired().HasConversion<string>()
                .HasDefaultValue(MergeStatus.NotStarted);
            builder.Property(e => e.MergeId).HasMaxLength(64);
            builder.Property(e => e.MergeFileName).HasMaxLength(200);
            builder.Property(e => e.MergeError).HasMaxLength(1000);
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);
            builder.Property(e => e.App).IsRequired().HasMaxLength(50);

            // NB: soft-delete (!IsDeleted) is combined with app scoping into ONE global query filter in
            // ApplicationDbContext.OnModelCreating — EF Core allows only a single query filter per entity.

            builder.HasMany(e => e.GeneticInspections)
                .WithOne(gi => gi.RawGeneticFile)
                .HasForeignKey(gi => gi.RawGeneticFileId);

            // Per-user uniqueness: a user cannot have two active uploads with the same
            // file name. Soft-deleted rows are excluded so a user can re-upload the same
            // name after deleting it.
            builder.HasIndex(e => new { e.App, e.CreatedBy, e.RawDataFileName })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");

            builder.ToTable("raw_genetic_files");
        }
    }
}
