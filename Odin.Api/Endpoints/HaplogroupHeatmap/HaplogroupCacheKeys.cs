namespace Odin.Api.Endpoints.HaplogroupHeatmap
{
    /// <summary>
    /// In-process cache keys for the haplogroup heatmap. The distribution response for a clade is the
    /// same for every user, so it's cached — and keyed by the latest import run id (the "token") so a
    /// fresh import implicitly invalidates every per-clade entry: the import job removes
    /// <see cref="ImportToken"/>, the next request recomputes the token, and the old keys age out by TTL.
    /// </summary>
    public static class HaplogroupCacheKeys
    {
        /// <summary>Cached id of the latest Completed import run; removed by the import job on success.</summary>
        public const string ImportToken = "haplo:import-token";

        public static string Distribution(int token, string clade) => $"haplo:dist:{token}:{clade}";
    }
}
