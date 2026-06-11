namespace Odin.Api.Data.Enums
{
    /// <summary>
    /// Outcome of the Y-DNA clade analysis run for a qpAdm genetic inspection. Persisted so the
    /// result (or the reason it is unavailable) is computed once at order time and cached, rather
    /// than recomputed on every result view.
    /// </summary>
    public enum CladeAnalysisStatus
    {
        /// <summary>Analysis ran on valid input that contained Y-chromosome data; clade payload is populated.</summary>
        Completed,

        /// <summary>The upload parsed fine but had no Y-chromosome SNP calls (e.g. autosomal-only or incomplete export).</summary>
        NoYData,

        /// <summary>The upload could not be used for Y-DNA (unsupported chip, truncated file, bad genome build, ...).</summary>
        InvalidData,

        /// <summary>The clade service was unreachable / reference data missing. Transient — eligible for re-analysis.</summary>
        Unavailable,

        /// <summary>Y-DNA does not apply to this kit (female sample has no Y chromosome). No service call made.</summary>
        NotApplicable
    }
}
