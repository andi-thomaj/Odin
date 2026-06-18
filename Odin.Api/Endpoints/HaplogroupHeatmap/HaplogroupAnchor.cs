using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;

namespace Odin.Api.Endpoints.HaplogroupHeatmap
{
    /// <summary>
    /// Resolves a clade to its <b>nearest named subclade</b> — the deepest ancestor that is a recognisable
    /// named clade (E-V13, R-M269, J-P58, …) — so every heatmap surface anchors on a well-populated,
    /// recognisable clade rather than the bare top-level letter or an ultra-deep terminal. The single
    /// source of truth for the named-subclade set, shared by the distribution and relative-frequency
    /// endpoints. Regenerate <see cref="NamedSubclades"/> on a YFull bump.
    /// </summary>
    public static class HaplogroupAnchor
    {
        /// <summary>The nearest named ancestor of <paramref name="clade"/> (inclusive), or the clade itself
        /// when it has no named ancestor / is not in the imported tree.</summary>
        public static async Task<string> ResolveAsync(
            ApplicationDbContext dbContext, string clade, CancellationToken cancellationToken = default)
        {
            var chain = await dbContext.Database
                .SqlQueryRaw<AncestorRow>(AncestorChainSql, clade)
                .ToListAsync(cancellationToken);
            return chain
                .Where(a => NamedSubclades.Contains(a.Id))
                .OrderBy(a => a.Depth) // depth 0 = the clade itself; smallest depth = most specific named ancestor
                .Select(a => a.Id)
                .FirstOrDefault() ?? clade;
        }

        // Ancestor chain (id + depth, depth 0 = the clade). {0} = clade; the table is lowercase (ToTable).
        public const string AncestorChainSql = """
            WITH RECURSIVE ancestors AS (
                SELECT "Id", "ParentId", 0 AS depth FROM y_haplogroup_tree_nodes WHERE "Id" = {0}
                UNION ALL
                SELECT n."Id", n."ParentId", a.depth + 1
                FROM y_haplogroup_tree_nodes n JOIN ancestors a ON n."Id" = a."ParentId"
            )
            SELECT "Id" AS "Id", depth AS "Depth" FROM ancestors ORDER BY depth
            """;

        // Recognisable "named subclade" YFull node ids (resolved from significant defining SNPs across all
        // haplogroups). The heatmap anchors a clade on the nearest of these, so users see e.g. E-V13 /
        // R-M269 / J-P58 rather than the bare letter or an ultra-deep terminal.
        public static readonly HashSet<string> NamedSubclades = new(StringComparer.OrdinalIgnoreCase)
        {
            "C", "C-M217", "C-M347", "C-M38", "C-M407", "C-M48", "C-M8", "D-M174", "D-M64.1",
            "E-M123", "E-M132", "E-M2", "E-M215", "E-M293", "E-M34", "E-M329", "E-M35", "E-M3895",
            "E-M78", "E-M84", "E-V12", "E-V13", "E-V16", "E-V22", "E-V6", "E-V65",
            "G", "G-L13", "G-L497", "G-M342", "G-M377", "G-M406", "G-P15", "G-P303", "G-U1",
            "H-M197", "H-M52", "H-M69", "H-M82",
            "I-DF29", "I-L158", "I-L161", "I-L621", "I-M223", "I-M423", "I-M436", "I-Z58", "I-Z63", "I1", "I2",
            "J-L283", "J-M102", "J-M158", "J-M205", "J-M241", "J-M410", "J-M67", "J-M68", "J-P58", "J1", "J2",
            "L", "L-L1307", "L-M27", "L-M317",
            "N", "N-L1026", "N-P43", "N-TAT", "N-VL29", "N-Z1936",
            "O-M119", "O-M122", "O-M134", "O-M268", "O-M7", "O-M95", "O-P201", "O-P49",
            "Q", "Q-L54", "Q-L56", "Q-M25", "Q-M3", "Q-M378",
            "R-DF27", "R-L21", "R-L23", "R-L51", "R-L52", "R-M198", "R-M222", "R-M269", "R-M417", "R-M458",
            "R-P312", "R-U106", "R-U152", "R-Z2103", "R-Z280", "R-Z282", "R-Z283", "R-Z93", "R1a", "R2",
            "T", "T-M70", "T-P77",
        };

        private sealed class AncestorRow
        {
            public string Id { get; set; } = string.Empty;
            public int Depth { get; set; }
        }
    }
}
