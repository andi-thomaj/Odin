using Odin.Api.Endpoints.G25PcaScatter;

namespace Odin.Api.Tests.G25PcaScatter;

public class G25PcaEngineTests
{
    private const int Dims = G25PcaEngine.Dims;

    // Deterministic synthetic cloud: variance is largest along dim 0, next-largest along dim 1, tiny
    // elsewhere — so the top-2 PCA axes must load primarily on dims 0 and 1 respectively.
    private static List<double[]> BuildStructuredCloud(int n)
    {
        var cloud = new List<double[]>(n);
        for (var i = 0; i < n; i++)
        {
            var v = new double[Dims];
            v[0] = (i - n / 2.0) * 1.0;          // wide spread
            v[1] = ((i % 7) - 3) * 0.1;           // moderate spread
            for (var j = 2; j < Dims; j++)
                v[j] = Math.Sin(i + j) * 1e-4;    // negligible, deterministic
            cloud.Add(v);
        }

        return cloud;
    }

    [Fact]
    public void Fit_ReturnsNull_WhenTooFewSamples()
    {
        var cloud = BuildStructuredCloud(5);
        Assert.Null(G25PcaEngine.Fit(cloud, minSamples: 10));
    }

    [Fact]
    public void Fit_AxesAreUnitLength_AndAlignWithTheHighVarianceDimensions()
    {
        var basis = G25PcaEngine.Fit(BuildStructuredCloud(200));
        Assert.NotNull(basis);

        // Unit-length principal axes.
        Assert.Equal(1.0, Norm(basis!.AxisX), 6);
        Assert.Equal(1.0, Norm(basis.AxisY), 6);

        // PC1 loads mostly on dim 0, PC2 mostly on dim 1.
        Assert.Equal(0, IndexOfMaxAbs(basis.AxisX));
        Assert.Equal(1, IndexOfMaxAbs(basis.AxisY));

        // Eigenvalues are ordered (PC1 explains at least as much variance as PC2).
        Assert.True(basis.EigenX >= basis.EigenY);
    }

    [Fact]
    public void Fit_SignConvention_IsDeterministic_AcrossRepeatedFits()
    {
        var cloud = BuildStructuredCloud(150);

        var a = G25PcaEngine.Fit(cloud);
        var b = G25PcaEngine.Fit(cloud);
        Assert.NotNull(a);
        Assert.NotNull(b);

        for (var j = 0; j < Dims; j++)
        {
            Assert.Equal(a!.Means[j], b!.Means[j], 12);
            Assert.Equal(a.AxisX[j], b.AxisX[j], 12);
            Assert.Equal(a.AxisY[j], b.AxisY[j], 12);
        }

        // The convention forces each axis's largest-magnitude loading positive.
        Assert.True(a!.AxisX[IndexOfMaxAbs(a.AxisX)] > 0);
        Assert.True(a.AxisY[IndexOfMaxAbs(a.AxisY)] > 0);
    }

    [Fact]
    public void Project_MatchesCenterThenDot_AndCentersTheCloudMeanAtOrigin()
    {
        var cloud = BuildStructuredCloud(120);
        var basis = G25PcaEngine.Fit(cloud);
        Assert.NotNull(basis);

        // Projecting an arbitrary sample equals the manual centered dot product with the basis axes.
        var sample = cloud[37];
        var (x, y) = G25PcaEngine.Project(sample, basis!);

        double mx = 0, my = 0;
        for (var j = 0; j < Dims; j++)
        {
            var centered = sample[j] - basis!.Means[j];
            mx += centered * basis.AxisX[j];
            my += centered * basis.AxisY[j];
        }

        Assert.Equal(mx, x, 9);
        Assert.Equal(my, y, 9);

        // The mean coordinate projects to (0, 0).
        var (cx, cy) = G25PcaEngine.Project(basis!.Means, basis);
        Assert.Equal(0.0, cx, 9);
        Assert.Equal(0.0, cy, 9);
    }

    private static double Norm(double[] v)
    {
        double s = 0;
        foreach (var x in v) s += x * x;
        return Math.Sqrt(s);
    }

    private static int IndexOfMaxAbs(double[] v)
    {
        var index = 0;
        var max = 0.0;
        for (var j = 0; j < v.Length; j++)
        {
            var a = Math.Abs(v[j]);
            if (a > max)
            {
                max = a;
                index = j;
            }
        }

        return index;
    }
}
