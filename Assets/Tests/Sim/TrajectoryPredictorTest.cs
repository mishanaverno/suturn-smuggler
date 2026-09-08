using System;
using System.Collections.Generic;
using DoublePrecision;
using NUnit.Framework;
using OuterSpace;
using OuterSpace.Sim;
using UnityEngine;

public class TrajectoryPredictorTest
{
    // Момент встречи с Реей задаётся, а траектория строится под него: подобрать перелёт
    // перебором дороже, а проверять надо не подбор, а поиск события.
    const double EncounterEpoch = 200000.0;
    const double LeadTime = 5400.0;

    SaturnTestWorld world;

    [SetUp]
    public void SetUp() => world = new SaturnTestWorld();

    [TearDown]
    public void TearDown() => world.Dispose();

    /// <summary>
    /// Встречный курс мимо Реи: в EncounterEpoch корабль ровно на границе её сферы влияния
    /// и идёт внутрь. Относительная скорость 17 км/с, ширина окна пролёта — около 7 минут.
    /// </summary>
    OrbitElements InboundToRhea()
    {
        SpaceObject rhea = world["rhea"];
        (Vector3d rheaR, Vector3d rheaV) = TrajectoryPredictor.BodyStateAt(rhea, EncounterEpoch);
        Vector3d approach = (-2.0 * rheaV).normalized;
        Vector3d side = Vector3d.Cross(approach, new Vector3d(0, 0, 1)).normalized;
        Vector3d offset = (-approach * 0.9 + side * 0.435) * rhea.SOI;
        return AstroDynamic.CalculateOrbitElements(rheaR + offset, -rheaV, world.saturn.MU, EncounterEpoch);
    }

    static PredictSettings Settings(double horizon, double maxStep = 3600.0) =>
        new() { horizon = horizon, maxStep = maxStep };

    List<TrajectoryPatch> Predict(OrbitElements orbit, SpaceObject central, double startEpoch, PredictSettings settings) =>
        TrajectoryPredictor.Predict(orbit, central, startEpoch, world.bodies, settings);

    static OrbitElements Circular(double mu, double radius, double meanAnomaly, double epoch, bool retrograde = false) => new()
    {
        semiMajorAxis = radius,
        eccentricity = 0.0,
        inclination = retrograde ? 180.0 : 0.0,
        longitudeOfAscendingNode = 0.0,
        argumentOfPeriapsis = 0.0,
        meanAnomalyAtEpoch = meanAnomaly,
        startEpoch = epoch,
        mu = mu,
    };

    /// <summary>Гоняет симуляцию шагами до первой смены центрального тела. Возвращает NaN, если её нет.</summary>
    double SimulateUntilTransition(SpaceObject ship, double from, double to, double step)
    {
        for (double epoch = from + step; epoch <= to; epoch += step)
        {
            if (world.Step(epoch, ship)) return epoch;
        }
        return double.NaN;
    }

    [Test]
    public void Prediction_MatchesSimulation_OnSOIEntry()
    {
        OrbitElements orbit = InboundToRhea();
        double start = EncounterEpoch - LeadTime;
        List<TrajectoryPatch> patches = Predict(orbit, world.saturn, start, Settings(2.0 * LeadTime));

        Assert.AreEqual(PatchEndReason.EnteredSOI, patches[0].EndReason);
        Assert.AreSame(world["rhea"], patches[0].NextCentral);

        SpaceObject ship = world.Put(world.saturn, orbit, start);
        double transition = SimulateUntilTransition(ship, start, start + 2.0 * LeadTime, 2.0);

        Assert.IsFalse(double.IsNaN(transition), "симуляция перехода не увидела");
        Assert.AreSame(world["rhea"], ship.centralBody);
        Assert.That(Math.Abs(transition - patches[0].EndEpoch), Is.LessThan(3.0), "момент входа");

        Vector3d predicted = TrajectoryPredictor.ShipStateAt(patches, transition).r;
        Assert.That((predicted - ship.simTransform.GLOBAL_R).magnitude / predicted.magnitude,
            Is.LessThan(1e-6), "точка входа");
    }

    [Test]
    public void Prediction_MatchesSimulation_OnSOIEscape()
    {
        SpaceObject titan = world["titan"];
        const double radius = 2.975e6;
        double speed = 1.5 * Math.Sqrt(titan.MU / radius);
        OrbitElements orbit = AstroDynamic.CalculateOrbitElements(
            new Vector3d(radius, 0, 0), new Vector3d(0, speed, 0), titan.MU, 0.0);

        List<TrajectoryPatch> patches = Predict(orbit, titan, 0.0, Settings(1.0e5));

        Assert.AreEqual(PatchEndReason.EscapedSOI, patches[0].EndReason);
        Assert.AreSame(world.saturn, patches[0].NextCentral);

        SpaceObject ship = world.Put(titan, orbit, 0.0);
        double transition = SimulateUntilTransition(ship, 0.0, 1.0e5, 5.0);

        Assert.IsFalse(double.IsNaN(transition), "симуляция выхода не увидела");
        Assert.AreSame(world.saturn, ship.centralBody);
        Assert.That(Math.Abs(transition - patches[0].EndEpoch), Is.LessThan(6.0), "момент выхода");

        Vector3d predicted = TrajectoryPredictor.ShipStateAt(patches, transition).r;
        Assert.That((predicted - ship.simTransform.GLOBAL_R).magnitude / predicted.magnitude,
            Is.LessThan(1e-6), "точка выхода");
    }

    [Test]
    public void CoarseUniformGridMissesEncounter_PredictorFindsIt()
    {
        OrbitElements orbit = InboundToRhea();
        SpaceObject rhea = world["rhea"];
        double start = EncounterEpoch - LeadTime;

        bool found = false;
        for (double epoch = start; epoch <= start + 2.0 * LeadTime; epoch += 3600.0)
        {
            Vector3d ship = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(orbit, epoch).r;
            Vector3d moon = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(rhea.orbitParams, epoch).r;
            if ((ship - moon).magnitude <= rhea.SOI) found = true;
        }
        Assert.IsFalse(found, "сетка с часовым шагом эту встречу не теряет — сценарий бесполезен");

        List<TrajectoryPatch> patches = Predict(orbit, world.saturn, start, Settings(2.0 * LeadTime));
        Assert.AreEqual(PatchEndReason.EnteredSOI, patches[0].EndReason);
        Assert.AreSame(rhea, patches[0].NextCentral);
        Assert.That(Math.Abs(patches[0].EndEpoch - EncounterEpoch), Is.LessThan(1.0));
    }

    [Test]
    public void EntryEpoch_DoesNotDependOnMaxStep()
    {
        OrbitElements orbit = InboundToRhea();
        double start = EncounterEpoch - LeadTime;
        double reference = double.NaN;

        foreach (double maxStep in new[] { 600.0, 1800.0, 3600.0, 7200.0 })
        {
            List<TrajectoryPatch> patches = Predict(orbit, world.saturn, start, Settings(2.0 * LeadTime, maxStep));
            Assert.AreEqual(PatchEndReason.EnteredSOI, patches[0].EndReason, $"maxStep = {maxStep}");
            if (double.IsNaN(reference)) reference = patches[0].EndEpoch;
            Assert.That(Math.Abs(patches[0].EndEpoch - reference), Is.LessThan(1.0), $"maxStep = {maxStep}");
        }
    }

    [Test]
    public void OrbitBetweenMoons_EndsAtHorizonWithSinglePatch()
    {
        // Между Реей (5.27e8) и Титаном (1.22e9): ни одной сферы влияния по дороге.
        OrbitElements orbit = Circular(world.saturn.MU, 8.0e8, 0.0, 0.0);
        List<TrajectoryPatch> patches = Predict(orbit, world.saturn, 0.0, Settings(30.0 * 86400.0));

        Assert.AreEqual(1, patches.Count);
        Assert.AreEqual(PatchEndReason.Horizon, patches[0].EndReason);
        Assert.IsNull(patches[0].NextCentral);
        Assert.AreEqual(30.0 * 86400.0, patches[0].EndEpoch, 1e-9);
    }

    [Test]
    public void EscapeEpoch_MatchesUniformScanOfRadius()
    {
        SpaceObject titan = world["titan"];
        const double radius = 2.975e6;
        double speed = 1.5 * Math.Sqrt(titan.MU / radius);
        OrbitElements orbit = AstroDynamic.CalculateOrbitElements(
            new Vector3d(radius, 0, 0), new Vector3d(0, speed, 0), titan.MU, 0.0);

        List<TrajectoryPatch> patches = Predict(orbit, titan, 0.0, Settings(1.0e5));

        // Порог тот же, что у рантайма: выход считается по SOI с гистерезисом.
        double threshold = titan.SOI * (1.0 + SOITransition.Hysteresis);
        double scan = double.NaN;
        for (double epoch = 0.0; epoch <= 1.0e5; epoch += 0.5)
        {
            if (AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(orbit, epoch).r.magnitude <= threshold) continue;
            scan = epoch;
            break;
        }

        Assert.IsFalse(double.IsNaN(scan), "по сетке выход не найден");
        Assert.That(Math.Abs(patches[0].EndEpoch - scan), Is.LessThan(1.0));
    }

    [Test]
    public void PeriapsisBelowSurface_EndsWithImpact()
    {
        SpaceObject rhea = world["rhea"];
        OrbitElements orbit = new()
        {
            semiMajorAxis = 1.0e6,
            eccentricity = 0.5,
            inclination = 0.0,
            longitudeOfAscendingNode = 0.0,
            argumentOfPeriapsis = 0.0,
            meanAnomalyAtEpoch = Math.PI,
            startEpoch = 0.0,
            mu = rhea.MU,
        };
        Assert.Less(orbit.semiMajorAxis * (1.0 - orbit.eccentricity), rhea.radius, "перицентр должен быть под поверхностью");

        List<TrajectoryPatch> patches = Predict(orbit, rhea, 0.0, Settings(86400.0));

        Assert.AreEqual(1, patches.Count);
        Assert.AreEqual(PatchEndReason.Impact, patches[0].EndReason);
        Assert.IsNull(patches[0].NextCentral);
        double impactRadius = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(orbit, patches[0].EndEpoch).r.magnitude;
        Assert.That(Math.Abs(impactRadius - rhea.radius), Is.LessThan(1.0e3));
    }

    [Test]
    public void SameInput_GivesBitwiseSameResult()
    {
        OrbitElements orbit = InboundToRhea();
        double start = EncounterEpoch - LeadTime;

        List<TrajectoryPatch> first = Predict(orbit, world.saturn, start, Settings(2.0 * LeadTime));
        List<TrajectoryPatch> second = Predict(orbit, world.saturn, start, Settings(2.0 * LeadTime));

        Assert.AreEqual(first.Count, second.Count);
        for (int i = 0; i < first.Count; i++)
        {
            Assert.AreEqual(first[i].EndEpoch, second[i].EndEpoch);
            Assert.AreEqual(first[i].EndReason, second[i].EndReason);
            Assert.AreSame(first[i].Central, second[i].Central);
            Assert.AreSame(first[i].NextCentral, second[i].NextCentral);
        }
    }

    [Test]
    public void PredictionFromManeuver_MatchesExecutedManeuver()
    {
        GameObject host = new("SimMono");
        SimMono.instance = host.AddComponent<SimMono>();
        SpaceObject titan = world["titan"];
        SpaceObject ship = world.Put(titan, Circular(titan.MU, 2.975e6, 0.0, 0.0), 0.0);

        Maneuver maneuver = new(ship, 100.0);
        maneuver.deltaLVLHVelocity = new Vector3d(1000.0, 0, 0);
        maneuver.CalcAndDraw();

        List<TrajectoryPatch> patches = Predict(maneuver.newOrbitParams, titan, maneuver.startEpoch, Settings(1.0e5));
        Assert.AreEqual(PatchEndReason.EscapedSOI, patches[0].EndReason);

        // Исполнение импульсного манёвра: с его эпохи корабль идёт по посчитанным элементам.
        world.Step(maneuver.startEpoch, ship);
        ship.orbitParams = maneuver.newOrbitParams;
        double transition = SimulateUntilTransition(ship, maneuver.startEpoch, 1.0e5, 5.0);

        UnityEngine.Object.DestroyImmediate(maneuver.GameObject);
        UnityEngine.Object.DestroyImmediate(host);
        SimMono.instance = null;

        Assert.IsFalse(double.IsNaN(transition), "манёвр исполнен, а перехода нет");
        Assert.AreSame(world.saturn, ship.centralBody);
        Assert.That(Math.Abs(transition - patches[0].EndEpoch), Is.LessThan(6.0));
    }

    [Test]
    public void Prefilter_LosesNothing()
    {
        System.Random random = new(20260908);
        for (int i = 0; i < 20; i++)
        {
            OrbitElements orbit = new()
            {
                semiMajorAxis = 2.0e8 + random.NextDouble() * 1.2e9,
                eccentricity = random.NextDouble() * 0.5,
                inclination = random.NextDouble() * 3.0,
                longitudeOfAscendingNode = random.NextDouble() * 360.0,
                argumentOfPeriapsis = random.NextDouble() * 360.0,
                meanAnomalyAtEpoch = random.NextDouble() * 2.0 * Math.PI,
                startEpoch = 0.0,
                mu = world.saturn.MU,
            };
            PredictSettings filtered = Settings(5.0 * 86400.0);
            PredictSettings full = Settings(5.0 * 86400.0);
            full.prefilter = false;

            List<TrajectoryPatch> a = Predict(orbit, world.saturn, 0.0, filtered);
            List<TrajectoryPatch> b = Predict(orbit, world.saturn, 0.0, full);

            Assert.AreEqual(b.Count, a.Count, $"орбита {i}");
            for (int p = 0; p < a.Count; p++)
            {
                Assert.AreEqual(b[p].EndReason, a[p].EndReason, $"орбита {i}, дуга {p}");
                Assert.AreSame(b[p].NextCentral, a[p].NextCentral, $"орбита {i}, дуга {p}");
                Assert.AreEqual(b[p].EndEpoch, a[p].EndEpoch, 1.0, $"орбита {i}, дуга {p}");
            }
        }
    }
}
