using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    /// <summary>
    /// One node of the YFull Y-DNA tree, imported from odin-tools-api for the haplogroup heatmap.
    /// <b>Shared reference data</b> (NOT <see cref="IAppScoped"/>) — the same tree serves every app.
    /// The <see cref="Id"/> is the YFull node id (terminal-mutation notation, e.g. <c>R-M269</c>, or
    /// YFull's short ids like <c>I1</c>); <see cref="ParentId"/> links toward the root (null at the top),
    /// enabling recursive-CTE subtree expansion and ancestor walks. <see cref="CentroidLat"/>/<see
    /// cref="CentroidLon"/> are the precomputed TMRCA-weighted ancestral location used to draw the
    /// migration path. Rows are fully replaced on each import (see <see cref="HaplogroupImportRun"/>).
    /// </summary>
    public class YHaplogroupTreeNode
    {
        public string Id { get; set; } = string.Empty;
        public string? ParentId { get; set; }

        /// <summary>YFull TMRCA age (years before present) — the migration time axis.</summary>
        public double? Tmrca { get; set; }
        public double? Formed { get; set; }

        /// <summary>Defining SNPs (truncated); informational only.</summary>
        public string? Snps { get; set; }

        /// <summary>Recursive TMRCA-weighted centroid of this clade's samples (null if no samples below).</summary>
        public double? CentroidLat { get; set; }
        public double? CentroidLon { get; set; }

        /// <summary>Geolocated samples in this node's whole subtree — drives migration weighting + display.</summary>
        public int SubtreeSampleCount { get; set; }

        /// <summary>Source release this row came from (e.g. AADR <c>v66.p1_HO</c>), for provenance.</summary>
        public string DatasetVersion { get; set; } = string.Empty;
    }

    public class YHaplogroupTreeNodeConfiguration : IEntityTypeConfiguration<YHaplogroupTreeNode>
    {
        public void Configure(EntityTypeBuilder<YHaplogroupTreeNode> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).HasMaxLength(120);
            builder.Property(e => e.ParentId).HasMaxLength(120);
            builder.Property(e => e.Snps).HasMaxLength(260);
            builder.Property(e => e.DatasetVersion).IsRequired().HasMaxLength(50);

            // Parent link is traversed by the distribution endpoint's recursive CTEs (subtree + ancestors).
            builder.HasIndex(e => e.ParentId);

            builder.ToTable("y_haplogroup_tree_nodes");
        }
    }
}
