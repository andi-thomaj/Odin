namespace Odin.Api.Endpoints.HaplogroupHeatmap.Models
{
    /// <summary>
    /// Geographic distribution of a Y-DNA clade for the result-page heatmap: ancient sample density
    /// binned by era, present-day (modern) density, a per-country modern breakdown for an optional
    /// choropleth, and the inferred migration path (ancestor-chain centroids ordered oldest → newest).
    /// </summary>
    public static class HaplogroupDistributionContract
    {
        public sealed class Response
        {
            /// <summary>The queried clade (a YFull node id, e.g. <c>R-M269</c>).</summary>
            public string Clade { get; set; } = string.Empty;

            /// <summary>The clade the heatmap is actually shown for — the nearest recognisable *named*
            /// subclade of the queried clade (e.g. a deep E-V13 sub-branch → <c>E-V13</c>). Equals
            /// <see cref="Clade"/> when the clade is already a named subclade or has no named ancestor.</summary>
            public string DisplayClade { get; set; } = string.Empty;

            /// <summary>False when the clade is not present in the imported tree (empty distribution).</summary>
            public bool Found { get; set; }

            public int TotalAncient { get; set; }
            public int TotalModern { get; set; }

            /// <summary>Ancient samples binned to ~1° cells per era (for a time-sliced geoheatmap/mapbubble).</summary>
            public List<EraBin> Ancient { get; set; } = [];

            /// <summary>Present-day samples binned to ~1° cells (the modern layer).</summary>
            public List<GeoBin> Modern { get; set; } = [];

            /// <summary>Modern counts by present-day country, for an optional choropleth.</summary>
            public List<CountryCount> ModernByCountry { get; set; } = [];

            /// <summary>Migration path: each ancestor clade's TMRCA-weighted centroid, oldest first.</summary>
            public List<MigrationPoint> Migration { get; set; } = [];

            /// <summary>Present-day frequency by country for the user's most-specific available clade
            /// (aggregated from Wikipedia, CC BY-SA) — drives the modern-frequency choropleth.</summary>
            public List<CountryFrequency> ModernFrequency { get; set; } = [];

            /// <summary>The clade the frequencies are for (an ancestor of the queried clade, e.g. <c>J</c>),
            /// or null if none of the user's lineage has frequency data.</summary>
            public string? ModernFrequencyClade { get; set; }
        }

        public sealed class CountryFrequency
        {
            public string Country { get; set; } = string.Empty;

            /// <summary>Highcharts world-map join key (ISO-3166-1 alpha-2, lowercase).</summary>
            public string HcKey { get; set; } = string.Empty;

            public double Percentage { get; set; }
            public int SampleSize { get; set; }
        }

        public sealed class EraBin
        {
            public double Lat { get; set; }
            public double Lon { get; set; }
            public string Era { get; set; } = string.Empty;
            public int Count { get; set; }
        }

        public sealed class GeoBin
        {
            public double Lat { get; set; }
            public double Lon { get; set; }
            public int Count { get; set; }
        }

        public sealed class CountryCount
        {
            public string Country { get; set; } = string.Empty;
            public int Count { get; set; }
        }

        public sealed class MigrationPoint
        {
            public string Clade { get; set; } = string.Empty;
            public double? Tmrca { get; set; }
            public double Lat { get; set; }
            public double Lon { get; set; }
            public int SampleCount { get; set; }
        }
    }
}
