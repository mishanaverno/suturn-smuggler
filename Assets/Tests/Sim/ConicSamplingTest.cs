using System;
using System.Collections.Generic;
using DoublePrecision;
using NUnit.Framework;
using OuterSpace;
using OuterSpace.Sim;
using UnityEngine;

/// <summary>
/// «Кривая, а не ломаная» формализовано так: угол между соседними точками, видимый из фокуса,
/// не больше пяти градусов. Критерий не зависит ни от масштаба, ни от эксцентриситета,
/// ни от горизонта прогноза — и ловит ровно тот веер хорд, из-за которого писалась выборка.
/// </summary>
public class ConicSamplingTest
{
    const int MaxPoints = 1024;
    const double Mu = 8.978e12; // Титан

    static OrbitElements Orbit(double eccentricity, double semiMajorAxis = 3.0e6) => new()
    {
        semiMajorAxis = semiMajorAxis,
        eccentricity = eccentricity,
        inclination = 15.0,
        longitudeOfAscendingNode = 40.0,
        argumentOfPeriapsis = 25.0,
        meanAnomalyAtEpoch = 0.3,
        startEpoch = 0.0,
        mu = Mu,
    };

    static double Period(OrbitElements orbit) => 2.0 * Math.PI * Math.Sqrt(Math.Pow(orbit.semiMajorAxis, 3) / orbit.mu);

    static double MaxAngle(List<Vector3d> points)
    {
        double worst = 0.0;
        for (int i = 1; i < points.Count; i++) worst = Math.Max(worst, Vector3d.Angle(points[i - 1], points[i]));
        return worst;
    }

    static double TotalAngle(List<Vector3d> points)
    {
        double total = 0.0;
        for (int i = 1; i < points.Count; i++) total += Vector3d.Angle(points[i - 1], points[i]);
        return total;
    }

    [Test]
    public void NeighbourPointsAreCloserThanFiveDegrees()
    {
        List<Vector3d> points = new();
        foreach (double eccentricity in new[] { 0.0, 0.3, 0.7, 0.95, 1.6 })
        {
            OrbitElements orbit = Orbit(eccentricity, eccentricity < 1.0 ? 3.0e6 : -3.0e6);
            double from = eccentricity < 1.0 ? 0.0 : -Math.PI;
            AstroDynamic.SampleConic(orbit, from, from + 2.0 * Math.PI, MaxPoints, points);

            Assert.Greater(points.Count, 2, $"e = {eccentricity}");
            Assert.That(MaxAngle(points), Is.LessThanOrEqualTo(5.0), $"e = {eccentricity}");
        }
    }

    [Test]
    public void ClosedArcIsDrawnOnceRegardlessOfSpan()
    {
        OrbitElements orbit = Orbit(0.3);
        List<Vector3d> points = new();
        AstroDynamic.SampleArc(orbit, 0.0, 240.0 * Period(orbit), MaxPoints, points);

        Assert.That(TotalAngle(points), Is.LessThanOrEqualTo(360.0 + 1e-6));
        Assert.LessOrEqual(points.Count, MaxPoints);
    }

    [Test]
    public void ArcEndsExactlyAtItsEpochs()
    {
        OrbitElements orbit = Orbit(0.6);
        double from = 100.0;
        double to = from + 0.25 * Period(orbit);
        List<Vector3d> points = new();
        AstroDynamic.SampleArc(orbit, from, to, MaxPoints, points);

        AssertClose(AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(orbit, from).r, points[0], 1e-9);
        AssertClose(AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(orbit, to).r, points[points.Count - 1], 1e-9);
    }

    [Test]
    public void SegmentsAreCommensurateOnEccentricOrbit()
    {
        OrbitElements orbit = Orbit(0.9);
        List<Vector3d> points = new();
        AstroDynamic.SampleConic(orbit, 0.0, 2.0 * Math.PI, MaxPoints, points);

        double longest = 0.0;
        double shortest = double.PositiveInfinity;
        for (int i = 1; i < points.Count; i++)
        {
            double length = (points[i] - points[i - 1]).magnitude;
            longest = Math.Max(longest, length);
            shortest = Math.Min(shortest, length);
        }
        Assert.That(longest / shortest, Is.LessThanOrEqualTo(10.0));
    }

    [Test]
    public void HyperbolicArcStaysInsideSOI()
    {
        using SaturnTestWorld world = new();
        SpaceObject titan = world["titan"];
        const double radius = 2.975e6;
        double speed = 1.5 * Math.Sqrt(titan.MU / radius);
        OrbitElements orbit = AstroDynamic.CalculateOrbitElements(
            new Vector3d(radius, 0, 0), new Vector3d(0, speed, 0), titan.MU, 0.0);

        List<TrajectoryPatch> patches = TrajectoryPredictor.Predict(
            orbit, titan, 0.0, world.bodies, new PredictSettings { horizon = 1.0e5 });
        Assert.AreEqual(PatchEndReason.EscapedSOI, patches[0].EndReason);

        List<Vector3d> points = new();
        AstroDynamic.SampleArc(patches[0].Orbit, patches[0].StartEpoch, patches[0].EndEpoch, MaxPoints, points);

        double limit = titan.SOI * (1.0 + SOITransition.Hysteresis);
        foreach (Vector3d point in points) Assert.LessOrEqual(point.magnitude, limit * 1.001);
    }

    [Test]
    public void ConicIsClippedAtSOI()
    {
        const double soi = 4.33e7; // Титан
        List<Vector3d> points = new();

        OrbitElements hyperbola = Orbit(1.6, -3.0e6);
        double limit = AstroDynamic.TrueAnomalyAtRadius(hyperbola, soi);
        AstroDynamic.SampleConic(hyperbola, -limit, limit, MaxPoints, points);

        foreach (Vector3d point in points) Assert.LessOrEqual(point.magnitude, soi * 1.001);
        // Дуга обрывается на границе сферы влияния, а не в случайной точке у асимптоты.
        Assert.That(Math.Abs(points[points.Count - 1].magnitude - soi) / soi, Is.LessThan(1e-9));

        // Орбита целиком внутри сферы влияния не обрезается.
        Assert.AreEqual(Math.PI, AstroDynamic.TrueAnomalyAtRadius(Orbit(0.3), soi), 1e-12);
        Assert.AreEqual(Math.PI, AstroDynamic.TrueAnomalyAtRadius(Orbit(0.3), double.PositiveInfinity), 1e-12);
    }

    [Test]
    public void OrbitRendererDrawsTheSamePoints()
    {
        GameObject host = new("Orbit");
        try
        {
            OrbitRenderer renderer = host.AddComponent<OrbitRenderer>();
            OrbitElements orbit = Orbit(0.4);
            renderer.parent = new OrbitStub(orbit, new Vector3d(1.0e8, 2.0e8, 0.0), double.PositiveInfinity);

            Vector3[] drawn = renderer.GetOrbitPoints(orbit);

            List<Vector3d> points = new();
            AstroDynamic.SampleConic(orbit, -Math.PI, Math.PI, MaxPoints, points);
            Assert.AreEqual(points.Count, drawn.Length);
            for (int i = 0; i < points.Count; i++)
            {
                Assert.AreEqual(SimView.ToScene(points[i] + renderer.parent.CenterPosition), drawn[i]);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [TestCase(0.0)]
    [TestCase(0.4)]
    [TestCase(0.9)]
    public void ApsisPositionsHaveExpectedRadiiAndAreOpposite(double eccentricity)
    {
        OrbitElements orbit = Orbit(eccentricity);
        Vector3d periapsis = AstroDynamic.PositionAtTrueAnomaly(orbit, 0.0);
        Vector3d apoapsis = AstroDynamic.PositionAtTrueAnomaly(orbit, Math.PI);

        Assert.AreEqual(orbit.semiMajorAxis * (1.0 - eccentricity), periapsis.magnitude, 1e-6);
        Assert.AreEqual(orbit.semiMajorAxis * (1.0 + eccentricity), apoapsis.magnitude, 1e-6);
        // Не через Vector3d.Angle: acos у 180° теряет точность до ~1e-6°.
        Assert.AreEqual(0.0, (periapsis.normalized + apoapsis.normalized).magnitude, 1e-12);
    }

    [Test]
    public void HyperbolicPeriapsisPositionHasExpectedRadius()
    {
        OrbitElements orbit = Orbit(1.6, -3.0e6);
        Vector3d periapsis = AstroDynamic.PositionAtTrueAnomaly(orbit, 0.0);

        Assert.AreEqual(orbit.semiMajorAxis * (1.0 - orbit.eccentricity), periapsis.magnitude, 1e-6);
    }

    [Test]
    public void PlaneNodesCrossReferencePlaneInOppositeDirections()
    {
        OrbitElements reference = Orbit(0.1);
        reference.inclination = 0.0;
        reference.longitudeOfAscendingNode = 0.0;
        reference.argumentOfPeriapsis = 0.0;

        OrbitElements orbit = Orbit(0.4);
        Assert.IsTrue(AstroDynamic.TryGetPlaneNodes(orbit, reference, out double ascending, out double descending));
        Assert.AreEqual(orbit.inclination, AstroDynamic.RelativeInclination(orbit, reference), 1e-12);

        const double step = 1e-5;
        Assert.AreEqual(0.0, AstroDynamic.PositionAtTrueAnomaly(orbit, ascending).z, 1e-6);
        Assert.Less(AstroDynamic.PositionAtTrueAnomaly(orbit, ascending - step).z, 0.0);
        Assert.Greater(AstroDynamic.PositionAtTrueAnomaly(orbit, ascending + step).z, 0.0);
        Assert.AreEqual(0.0, AstroDynamic.PositionAtTrueAnomaly(orbit, descending).z, 1e-6);
        Assert.Greater(AstroDynamic.PositionAtTrueAnomaly(orbit, descending - step).z, 0.0);
        Assert.Less(AstroDynamic.PositionAtTrueAnomaly(orbit, descending + step).z, 0.0);
    }

    [Test]
    public void CoplanarOrbitHasNoDistinctNodes()
    {
        OrbitElements orbit = Orbit(0.4);
        Assert.IsFalse(AstroDynamic.TryGetPlaneNodes(orbit, orbit, out _, out _));
    }

    [Test]
    public void NearParabolicOrbitDoesNotThrow()
    {
        List<Vector3d> points = new();
        foreach (double eccentricity in new[] { 1.0 - 1e-10, 1.0, 1.0 + 1e-10 })
        {
            OrbitElements orbit = Orbit(eccentricity);
            Assert.DoesNotThrow(() => AstroDynamic.SampleConic(orbit, 0.0, 2.0 * Math.PI, MaxPoints, points));
            Assert.DoesNotThrow(() => AstroDynamic.SampleArc(orbit, 0.0, 1000.0, MaxPoints, points));
        }
    }

    static void AssertClose(Vector3d expected, Vector3d actual, double relativeTolerance)
    {
        Assert.That((actual - expected).magnitude, Is.LessThanOrEqualTo(expected.magnitude * relativeTolerance),
            $"expected {expected}, got {actual}");
    }

    class OrbitStub : IHasOrbit
    {
        public OrbitElements OrbitParams { get; }
        public Vector3d CenterPosition { get; }
        public double CentralSOI { get; }

        public OrbitStub(OrbitElements orbit, Vector3d center, double soi)
        {
            OrbitParams = orbit;
            CenterPosition = center;
            CentralSOI = soi;
        }
    }
}
