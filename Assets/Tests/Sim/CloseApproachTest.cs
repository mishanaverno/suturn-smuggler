using System;
using System.Collections.Generic;
using DoublePrecision;
using NUnit.Framework;
using OuterSpace;
using OuterSpace.Sim;

public class CloseApproachTest
{
    const double ShipRadius = 8.0e8;
    const double StationRadius = 8.01e8;
    const double ConjunctionEpoch = 15000.0;
    const double Horizon = 30000.0;

    SaturnTestWorld world;

    [SetUp]
    public void SetUp() => world = new SaturnTestWorld();

    [TearDown]
    public void TearDown() => world.Dispose();

    static OrbitElements Circular(double mu, double radius, double meanAnomaly, bool retrograde = false) => new()
    {
        semiMajorAxis = radius,
        eccentricity = 0.0,
        inclination = retrograde ? 180.0 : 0.0,
        longitudeOfAscendingNode = 0.0,
        argumentOfPeriapsis = 0.0,
        meanAnomalyAtEpoch = meanAnomaly,
        startEpoch = 0.0,
        mu = mu,
    };

    /// <summary>
    /// Корабль идёт по круговой орбите навстречу станции на почти такой же: минимум расстояния
    /// приходится на ConjunctionEpoch и равен разности радиусов — геометрия известна заранее.
    /// </summary>
    OrbitElements ShipOrbit()
    {
        double n = Math.Sqrt(world.saturn.MU / Math.Pow(ShipRadius, 3));
        return Circular(world.saturn.MU, ShipRadius, -2.0 * n * ConjunctionEpoch, true);
    }

    OrbitElements StationOrbit() => Circular(world.saturn.MU, StationRadius, 0.0);

    static PredictSettings Settings(double maxStep = 3600.0) => new() { horizon = Horizon, maxStep = maxStep };

    List<TrajectoryPatch> Predict(OrbitElements orbit, SpaceObject central, PredictSettings settings) =>
        TrajectoryPredictor.Predict(orbit, central, 0.0, world.bodies, settings);

    static Vector3d PositionAt(OrbitElements orbit, double epoch) =>
        AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(orbit, epoch).r;

    /// <summary>Эталон: грубая равномерная сетка по всему интервалу и очень мелкая вокруг минимума.</summary>
    static (double epoch, double distance) BruteForceMinimum(Func<double, double> separation, double from, double to)
    {
        const double Coarse = 1.0;
        const double Fine = 0.01;
        double bestEpoch = from;
        double best = separation(from);
        for (double epoch = from; epoch <= to; epoch += Coarse)
        {
            double distance = separation(epoch);
            if (distance >= best) continue;
            best = distance;
            bestEpoch = epoch;
        }
        for (double epoch = bestEpoch - Coarse; epoch <= bestEpoch + Coarse; epoch += Fine)
        {
            double distance = separation(epoch);
            if (distance >= best) continue;
            best = distance;
            bestEpoch = epoch;
        }
        return (bestEpoch, best);
    }

    CloseApproach Closest(List<CloseApproach> approaches)
    {
        Assert.IsNotEmpty(approaches, "сближение не найдено");
        CloseApproach best = approaches[0];
        foreach (CloseApproach approach in approaches)
        {
            if (approach.Distance < best.Distance) best = approach;
        }
        return best;
    }

    [Test]
    public void ClosestApproach_MatchesFineUniformGrid()
    {
        OrbitElements ship = ShipOrbit();
        OrbitElements station = StationOrbit();
        SpaceObject target = world.Put(world.saturn, station, 0.0);

        List<TrajectoryPatch> patches = Predict(ship, world.saturn, Settings());
        CloseApproach approach = Closest(TrajectoryPredictor.FindCloseApproaches(patches, target, Settings()));

        (double epoch, double distance) = BruteForceMinimum(
            t => (PositionAt(ship, t) - PositionAt(station, t)).magnitude, 0.0, Horizon);

        Assert.That(Math.Abs(approach.Epoch - epoch), Is.LessThan(1.0), "момент сближения");
        Assert.That(Math.Abs(approach.Distance - distance), Is.LessThan(1.0), "расстояние");

        Vector3d relativeVelocity =
            AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(ship, approach.Epoch).v -
            AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(station, approach.Epoch).v;
        Assert.That(Math.Abs(approach.RelativeSpeed - relativeVelocity.magnitude), Is.LessThan(1.0), "относительная скорость");
    }

    [Test]
    public void ClosestApproach_DoesNotDependOnMaxStep()
    {
        OrbitElements ship = ShipOrbit();
        SpaceObject target = world.Put(world.saturn, StationOrbit(), 0.0);
        double referenceEpoch = double.NaN;
        double referenceDistance = 0.0;

        foreach (double maxStep in new[] { 600.0, 1800.0, 3600.0, 7200.0 })
        {
            PredictSettings settings = Settings(maxStep);
            List<TrajectoryPatch> patches = Predict(ship, world.saturn, settings);
            CloseApproach approach = Closest(TrajectoryPredictor.FindCloseApproaches(patches, target, settings));

            if (double.IsNaN(referenceEpoch))
            {
                referenceEpoch = approach.Epoch;
                referenceDistance = approach.Distance;
                continue;
            }
            Assert.That(Math.Abs(approach.Epoch - referenceEpoch), Is.LessThan(1.0), $"maxStep = {maxStep}");
            Assert.That(Math.Abs(approach.Distance - referenceDistance), Is.LessThan(1.0), $"maxStep = {maxStep}");
        }
    }

    [Test]
    public void ClosestApproach_MatchesSimulation()
    {
        OrbitElements shipOrbit = ShipOrbit();
        SpaceObject target = world.Put(world.saturn, StationOrbit(), 0.0);
        List<TrajectoryPatch> patches = Predict(shipOrbit, world.saturn, Settings());
        CloseApproach approach = Closest(TrajectoryPredictor.FindCloseApproaches(patches, target, Settings()));

        SpaceObject ship = world.Put(world.saturn, shipOrbit, 0.0);
        double bestEpoch = 0.0;
        double best = double.PositiveInfinity;
        for (double epoch = 0.0; epoch <= Horizon; epoch += 1.0)
        {
            world.Step(epoch, ship, target);
            double distance = (ship.simTransform.GLOBAL_R - target.simTransform.GLOBAL_R).magnitude;
            if (distance >= best) continue;
            best = distance;
            bestEpoch = epoch;
        }

        Assert.That(Math.Abs(approach.Epoch - bestEpoch), Is.LessThan(2.0), "момент сближения");
        Assert.That(Math.Abs(approach.Distance - best) / best, Is.LessThan(1.0e-3), "расстояние");
    }

    [Test]
    public void TargetInsideAnotherSOI_UsesAbsolutePositions()
    {
        // Корабль вокруг Сатурна, станция вокруг Реи: расстояние можно посчитать, только
        // собрав абсолютные положения по цепочке родителей. Корабль идёт встречным курсом
        // и на середине горизонта проходит через сферу влияния Реи.
        SpaceObject rhea = world["rhea"];
        OrbitElements stationOrbit = Circular(rhea.MU, 1.0e6, 0.0);
        SpaceObject station = world.Put(rhea, stationOrbit, 0.0);

        (Vector3d rheaPosition, Vector3d rheaVelocity) = TrajectoryPredictor.BodyStateAt(rhea, ConjunctionEpoch);
        Vector3d approachDirection = (-2.0 * rheaVelocity).normalized;
        Vector3d side = Vector3d.Cross(approachDirection, new Vector3d(0, 0, 1)).normalized;
        Vector3d offset = (-approachDirection * 0.9 + side * 0.435) * rhea.SOI;
        OrbitElements ship = AstroDynamic.CalculateOrbitElements(
            rheaPosition + offset, -rheaVelocity, world.saturn.MU, ConjunctionEpoch);

        PredictSettings settings = Settings();
        List<TrajectoryPatch> patches = Predict(ship, world.saturn, settings);
        Assert.Greater(patches.Count, 1, "корабль должен войти в сферу влияния Реи");
        CloseApproach approach = Closest(TrajectoryPredictor.FindCloseApproaches(patches, station, settings));

        double Separation(double epoch)
        {
            Vector3d target = PositionAt(rhea.orbitParams, epoch) + PositionAt(stationOrbit, epoch);
            return (TrajectoryPredictor.ShipStateAt(patches, epoch).r - target).magnitude;
        }

        (double epoch, double distance) = BruteForceMinimum(Separation, patches[0].StartEpoch, patches[patches.Count - 1].EndEpoch);

        Assert.That(Math.Abs(approach.Epoch - epoch), Is.LessThan(1.0), "момент сближения");
        Assert.That(Math.Abs(approach.Distance - distance), Is.LessThan(1.0), "расстояние");
        Assert.Less(approach.Distance, rhea.SOI, "сближение со станцией у Реи, а не с чем попало");
    }

    [Test]
    public void CoorbitalTarget_ReportsNoApproach()
    {
        OrbitElements ship = Circular(world.saturn.MU, ShipRadius, 0.0);
        SpaceObject target = world.Put(world.saturn, Circular(world.saturn.MU, ShipRadius, 1.0e-4), 0.0);

        List<TrajectoryPatch> patches = Predict(ship, world.saturn, Settings());
        Assert.IsEmpty(TrajectoryPredictor.FindCloseApproaches(patches, target, Settings()));
    }

    [Test]
    public void CentralBodyAsTarget_ReportsNoApproach()
    {
        OrbitElements ship = Circular(world.saturn.MU, ShipRadius, 0.0);
        List<TrajectoryPatch> patches = Predict(ship, world.saturn, Settings());
        Assert.IsEmpty(TrajectoryPredictor.FindCloseApproaches(patches, world.saturn, Settings()));
    }
}
