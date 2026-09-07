using NUnit.Framework;
using OuterSpace.Sim;
using DoublePrecision;
using OuterSpace;

public class AstroDynamicTest
{

    const double TOLERANCE = 1e-5; // Допуск по большой полуоси (метры)

    double EarthMu = 398600441800000.0;
    private OrbitElements GetTestOrbitEquatorial()
    {
        return new()
        {
            semiMajorAxis = 20000000.0,
            mu = EarthMu,
            eccentricity = 0.6,
            inclination = 0,
            longitudeOfAscendingNode = 0,
            argumentOfPeriapsis = 0,
            startEpoch = 0,
            meanAnomalyAtEpoch = 0
        };
    }
    private OrbitElements GetTestOrbitHiperbolic()
    {
        return new()
        {
            semiMajorAxis = -10000000.0,
            mu = EarthMu,
            eccentricity = 1.6,
            inclination = 0,
            longitudeOfAscendingNode = 0,
            argumentOfPeriapsis = 0,
            startEpoch = 0,
            meanAnomalyAtEpoch = 0
        };
    }

    [Test]
    public void RVEquatorial0()
    {
        OrbitElements o = GetTestOrbitEquatorial();
        Vector3d er = new(7999995.023425903, 8932.884993630298, 0.0);
        Vector3d ev = new(-6.228129640397754, 8928.6071, 0.0);
        new VectorsTestCase(o, 1, er, ev).Run();
    }
    [Test]
    public void RVHyperbolic()
    {
        OrbitElements o = GetTestOrbitHiperbolic();
        Vector3d er = new(5999994.5, 13142.5, 0);
        Vector3d ev = new(-20.2, 13142.3, 0);
        new VectorsTestCase(o, 1, er, ev).Run();
    }
}

public class VectorsTestCase
{
    public OrbitElements elements;
    public double t;
    public Vector3d expected_r, expected_v;
    public VectorsTestCase(OrbitElements elements, double t, Vector3d expected_r, Vector3d expected_v)
    {
        this.elements = elements;
        this.t = t;
        this.expected_r = expected_r;
        this.expected_v = expected_v;
    }
    public void Run()
    {
        (Vector3d actual_r, Vector3d actual_v) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(elements, t, true);
        AssertV(expected_v, actual_v);
        AssertR(expected_r, actual_r);
    }
    private void AssertR(Vector3d expected, Vector3d actual)
    {
        Assert.AreEqual(expected.x, actual.x, 1e-5, $"R x not equal");
        Assert.AreEqual(expected.y, actual.y, 1e-5, $"R y not equal");
        Assert.AreEqual(expected.z, actual.z, 1e-5, $"R z not equal");
    }
    private void AssertV(Vector3d expected, Vector3d actual)
    {
        Assert.AreEqual(expected.x, actual.x, 1e-3, $"V x not equal");
        Assert.AreEqual(expected.y, actual.y, 1e-3, $"V y not equal");
        Assert.AreEqual(expected.z, actual.z, 1e-3, $"V z not equal");
    }
}