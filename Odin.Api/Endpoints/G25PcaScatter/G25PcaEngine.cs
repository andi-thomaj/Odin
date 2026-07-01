using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace Odin.Api.Endpoints.G25PcaScatter;

/// <summary>
/// A fitted 2D PCA basis: the feature means and the top-2 principal axes (each a Dims-length loading
/// vector). Projecting a coordinate is <c>((coord - Means) · AxisX, (coord - Means) · AxisY)</c>.
/// The sign convention is baked into <see cref="AxisX"/>/<see cref="AxisY"/> so server and client agree.
/// </summary>
public sealed record G25PcaBasis(
    double[] Means,
    double[] AxisX,
    double[] AxisY,
    double EigenX,
    double EigenY,
    bool NearDegenerate);

/// <summary>
/// Fits a per-era PCA over the seeded G25 reference cloud and projects points into its top-2 subspace.
/// G25 coordinates are already 25 global principal components; here we recompute a PCA on a single era's
/// samples so that era's populations spread out on the 2D plot, then project the user's own coordinate
/// into the same basis. Uses MathNet.Numerics for the (tiny 25x25) symmetric eigendecomposition.
/// </summary>
public static class G25PcaEngine
{
    public const int Dims = 25;

    // Relative eigenvalue gap below which the top-2 axes are treated as near-degenerate (an axis swap a
    // sign convention can't fix) — surfaced for logging, not fatal.
    private const double DegenerateGapRatio = 0.02;

    /// <summary>
    /// Fit a 2D PCA basis from a set of <see cref="Dims"/>-dimensional coordinates. Returns null when
    /// there are too few samples, or the cloud is rank-deficient in the second axis, to fit a stable
    /// 2-component projection. The returned axes carry a deterministic sign convention (the component
    /// with the largest absolute loading is forced positive) so repeated fits never mirror.
    /// </summary>
    public static G25PcaBasis? Fit(IReadOnlyList<double[]> coordinates, int minSamples = 10)
    {
        var n = coordinates.Count;
        if (n < minSamples || n < 3) return null;

        // Column means.
        var means = new double[Dims];
        foreach (var c in coordinates)
            for (var j = 0; j < Dims; j++)
                means[j] += c[j];
        for (var j = 0; j < Dims; j++)
            means[j] /= n;

        // Centered data matrix (n x Dims).
        var centered = DenseMatrix.Create(n, Dims, 0.0);
        for (var i = 0; i < n; i++)
        {
            var c = coordinates[i];
            for (var j = 0; j < Dims; j++)
                centered[i, j] = c[j] - means[j];
        }

        // Covariance = Xcᵀ·Xc / (n-1)  (Dims x Dims, symmetric).
        var covariance = centered.TransposeThisAndMultiply(centered) / (n - 1.0);
        var evd = covariance.Evd(Symmetricity.Symmetric);

        // MathNet returns eigenvalues ascending, with eigenvectors as matching columns; the top-2 are
        // therefore the last two columns.
        var lambda1 = evd.EigenValues[Dims - 1].Real;
        var lambda2 = evd.EigenValues[Dims - 2].Real;
        var lambda3 = evd.EigenValues[Dims - 3].Real;
        if (lambda2 <= 1e-12) return null; // no stable second axis

        var axisX = evd.EigenVectors.Column(Dims - 1).ToArray();
        var axisY = evd.EigenVectors.Column(Dims - 2).ToArray();
        ApplySignConvention(axisX);
        ApplySignConvention(axisY);

        var scale = Math.Max(lambda1, 1e-12);
        var nearDegenerate = (lambda1 - lambda2) / scale < DegenerateGapRatio
                             || (lambda2 - lambda3) / scale < DegenerateGapRatio;

        return new G25PcaBasis(means, axisX, axisY, lambda1, lambda2, nearDegenerate);
    }

    /// <summary>Project a single coordinate into the basis: center, then dot with each axis.</summary>
    public static (double X, double Y) Project(double[] coordinate, G25PcaBasis basis)
    {
        double x = 0, y = 0;
        for (var j = 0; j < Dims; j++)
        {
            var centered = coordinate[j] - basis.Means[j];
            x += centered * basis.AxisX[j];
            y += centered * basis.AxisY[j];
        }

        return (x, y);
    }

    // Force the axis's largest-magnitude loading positive so the eigenvector's arbitrary sign is
    // deterministic across recomputations (otherwise the plot — and the client-projected user point —
    // could mirror between cold fits).
    private static void ApplySignConvention(double[] axis)
    {
        var maxIndex = 0;
        var maxAbs = 0.0;
        for (var j = 0; j < axis.Length; j++)
        {
            var abs = Math.Abs(axis[j]);
            if (abs > maxAbs)
            {
                maxAbs = abs;
                maxIndex = j;
            }
        }

        if (axis[maxIndex] >= 0) return;
        for (var j = 0; j < axis.Length; j++)
            axis[j] = -axis[j];
    }
}
