namespace Odin.Api.Endpoints.HaplogroupHeatmap.Models
{
    /// <summary>
    /// A smooth, kernel-interpolated <b>relative-frequency surface</b> (HRAS-style) for a clade — the
    /// 3rd heatmap mode. Two layers: <c>ancient</c> is the sample ratio (clade ÷ all ancient samples)
    /// computed from our coordinate-level data; <c>modern</c> interpolates the country-level frequency
    /// percentages. The grid is computed live by odin-tools-api; this endpoint anchors the clade to its
    /// nearest named subclade, proxies the call, and caches the result.
    /// </summary>
    public static class RelativeFrequencyContract
    {
        public sealed class Response
        {
            /// <summary>The queried clade (a YFull node id).</summary>
            public string Clade { get; set; } = string.Empty;

            /// <summary>The clade the surface is actually computed for — the nearest named subclade of
            /// <see cref="Clade"/> (same rule as the distribution's DisplayClade).</summary>
            public string DisplayClade { get; set; } = string.Empty;

            /// <summary>False when the clade is not present in the imported tree (empty surface).</summary>
            public bool Found { get; set; }

            /// <summary>'ancient' (sample ratio) or 'modern' (interpolated country %).</summary>
            public string Layer { get; set; } = string.Empty;

            /// <summary>Gaussian kernel bandwidth used, in km (the UI's "Radius").</summary>
            public double RadiusKm { get; set; }

            /// <summary>Grid spacing in degrees — drives the geoheatmap's colsize/rowsize.</summary>
            public double CellSize { get; set; }

            /// <summary>Modern only: the nearest ancestor clade that had frequency data (e.g. J-Z1865 → J1).
            /// Null for the ancient layer or when no ancestor has frequency data.</summary>
            public string? FrequencyClade { get; set; }

            /// <summary>Maximum cell value (%), for scaling the colour axis.</summary>
            public double MaxValue { get; set; }

            /// <summary>Clade ancient samples (ancient) or country points (modern) behind the surface.</summary>
            public int CladeCount { get; set; }

            /// <summary>All ancient samples (ancient) or total study n (modern) behind the surface.</summary>
            public int TotalCount { get; set; }

            /// <summary>The interpolated surface: one value per kept grid cell.</summary>
            public List<Cell> Cells { get; set; } = [];
        }

        public sealed class Cell
        {
            public double Lat { get; set; }
            public double Lon { get; set; }

            /// <summary>% relative frequency at this cell.</summary>
            public double Value { get; set; }
        }
    }
}
