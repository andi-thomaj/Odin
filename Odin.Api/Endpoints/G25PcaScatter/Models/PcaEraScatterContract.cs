namespace Odin.Api.Endpoints.G25PcaScatter.Models;

public class PcaEraScatterContract
{
    public class Response
    {
        public int EraId { get; set; }
        public required string EraName { get; set; }

        // Null when the era has too few samples to fit a stable 2D PCA — the FE shows an info message.
        public BasisDto? Basis { get; set; }

        // Downsampled individual reference points (already projected to 2D by the fitted basis).
        public List<PointDto> Points { get; set; } = [];

        // Per-population centroids computed from the FULL cloud (used for labels + a legible mobile view).
        public List<CentroidDto> Centroids { get; set; } = [];

        public int TotalSamples { get; set; }
        public int PlottedSamples { get; set; }
    }

    /// <summary>
    /// The projection basis so the FE can plot the user's own coordinate in the same space:
    /// <c>x = (coord - Means) · AxisX</c>, <c>y = (coord - Means) · AxisY</c>.
    /// </summary>
    public class BasisDto
    {
        public required double[] Means { get; set; }
        public required double[] AxisX { get; set; }
        public required double[] AxisY { get; set; }
    }

    public class PointDto
    {
        public required string Label { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class CentroidDto
    {
        public required string Label { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public int SampleCount { get; set; }

        // The population's 25-dim mean (full G25 space), so the FE can rank clusters by genetic distance
        // to the user's coordinate (the same metric as the Distance tab) and preselect the closest ones.
        public required double[] Coordinates { get; set; }
    }
}
