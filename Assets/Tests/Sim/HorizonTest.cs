using System.Collections.Generic;
using DoublePrecision;
using NUnit.Framework;
using OuterSpace;
using OuterSpace.Sim;

public class HorizonTest
{
    SaturnTestWorld world;

    [SetUp]
    public void SetUp() => world = new SaturnTestWorld();

    [TearDown]
    public void TearDown() => world.Dispose();

    static OrbitElements Circular(double mu, double radius) => new()
    {
        semiMajorAxis = radius,
        eccentricity = 0.0,
        inclination = 0.0,
        longitudeOfAscendingNode = 0.0,
        argumentOfPeriapsis = 0.0,
        meanAnomalyAtEpoch = 0.0,
        startEpoch = 0.0,
        mu = mu,
    };

    [Test]
    public void Horizon_IsFivePeriods_OnEveryClosedOrbit()
    {
        PredictSettings settings = new();
        SpaceObject titan = world["titan"];
        foreach (double radius in new[] { 2.975e6, 1.0e7, 4.0e7 })
        {
            OrbitElements orbit = Circular(titan.MU, radius);
            Assert.AreEqual(settings.horizonPeriods * AstroDynamic.Period(orbit), settings.HorizonFor(orbit), 1e-6);
        }
        Assert.AreEqual(5.0, settings.horizonPeriods);
    }

    /// <summary>
    /// Прибор обязан одинаково заранее показывать событие для траектории корабля и для
    /// траектории манёвра, посчитанных от одной орбиты, — а горизонт задаётся не вызывающим,
    /// а самим кешем прогноза.
    /// </summary>
    [Test]
    public void ShipAndManeuver_ShareHorizon_OnTheSameOrbit()
    {
        SpaceObject titan = world["titan"];
        OrbitElements orbit = Circular(titan.MU, 2.975e6);

        TrajectoryCache ship = new();
        TrajectoryCache maneuver = new();
        ship.Update(orbit, titan, 0.0, null);
        maneuver.Update(orbit, titan, 0.0, null);

        Assert.AreEqual(ship.settings.horizon, maneuver.settings.horizon);
        Assert.AreEqual(ship.patches[0].EndEpoch, maneuver.patches[0].EndEpoch, 1e-6);
    }

    [Test]
    public void OpenOrbit_UsesConstant_AndReachesSOIExit()
    {
        SpaceObject titan = world["titan"];
        (Vector3d r, Vector3d v) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(Circular(titan.MU, 2.975e6), 0.0);
        // Полтора круговых: заведомо гиперболический уход от Титана.
        OrbitElements escape = AstroDynamic.CalculateOrbitElements(r, v * 1.5, titan.MU, 0.0);
        Assert.Greater(escape.eccentricity, 1.0);

        PredictSettings settings = new();
        settings.horizon = settings.HorizonFor(escape);
        Assert.AreEqual(settings.openOrbitHorizon, settings.horizon);
        Assert.Less(settings.openOrbitHorizon, 30.0 * 86400.0);

        List<TrajectoryPatch> patches = TrajectoryPredictor.Predict(escape, titan, 0.0, world.bodies, settings);
        Assert.AreEqual(PatchEndReason.EscapedSOI, patches[0].EndReason);
        Assert.Less(patches[0].EndEpoch, settings.openOrbitHorizon);
    }
}
