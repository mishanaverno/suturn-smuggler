using DoublePrecision;
using Game;
using NUnit.Framework;
using OuterSpace;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using UnityEngine;

public class WarpLimitTest
{
    // Кадр при 60 fps: на нём меряется страховочный предел по окну пролёта.
    const double FrameSeconds = 1.0 / 60.0;
    const uint Requested = 100000;
    const double EncounterEpoch = 200000.0;
    const double LeadTime = 3600.0;

    SaturnTestWorld world;

    [SetUp]
    public void SetUp() => world = new SaturnTestWorld();

    [TearDown]
    public void TearDown() => world.Dispose();

    [Test]
    public void Ladder_IsMonotone_AndJumpsByDecade()
    {
        for (int i = 1; i < TimeToggler.Ladder.Count; i++)
        {
            Assert.Greater(TimeToggler.Ladder[i], TimeToggler.Ladder[i - 1]);
        }
        foreach (uint speed in new uint[] { 1, 2, 5, 10, 20, 50, 100, 100000 })
        {
            Assert.Contains(speed, (uint[])TimeToggler.Ladder, $"нет ступени x{speed}");
        }

        GameObject host = new("TimeToggler");
        try
        {
            TimeToggler toggler = host.AddComponent<TimeToggler>();
            uint before = toggler.Current;
            Assert.AreEqual(10 * before, toggler.Shift(TimeToggler.StepsPerDecade).Current);
            Assert.AreEqual(before, toggler.Shift(-TimeToggler.StepsPerDecade).Current);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void Allowed_StaysWithinLead_AndNeverBelowOne()
    {
        foreach (double timeToEvent in new[] { 0.0, 1.0, 5.0, 60.0, 3600.0, 1e9 })
        {
            double allowed = WarpLimit.Allowed(Requested, timeToEvent);
            Assert.LessOrEqual(allowed, Mathd.Max(timeToEvent / WarpLimit.LeadSeconds, 1.0) + 1e-9);
            Assert.GreaterOrEqual(allowed, 1.0);
            Assert.LessOrEqual(allowed, Requested);
        }
    }

    [Test]
    public void Allowed_FallsMonotonically_TowardsEvent()
    {
        double previous = double.PositiveInfinity;
        for (double timeToEvent = 7200.0; timeToEvent >= 0.0; timeToEvent -= 60.0)
        {
            double allowed = WarpLimit.Allowed(Requested, timeToEvent);
            Assert.LessOrEqual(allowed, previous);
            previous = allowed;
        }
        Assert.AreEqual(1.0, previous);
    }

    [Test]
    public void Allowed_ReturnsToRequested_AfterEventIsPassed()
    {
        Assert.AreEqual(1.0, WarpLimit.Allowed(Requested, 0.0));
        // Событие пройдено: ближайшее следующее далеко, и ограничение снимается целиком.
        Assert.AreEqual(Requested, WarpLimit.Allowed(Requested, Requested * WarpLimit.LeadSeconds));
    }

    /// <summary>Главный тест: сквозь сферу влияния Мимаса корабль не проходит ни при какой перемотке.</summary>
    [Test]
    public void ShipNeverSkips_MimasSOI_AtAnyWarp()
    {
        SpaceObject mimas = world["mimas"];
        double start = EncounterEpoch - LeadTime;
        Ship ship = world.PutShip(world.saturn, InboundTo(mimas, EncounterEpoch), start);

        double epoch = start;
        bool transitioned = false;
        while (epoch < EncounterEpoch + LeadTime && !transitioned)
        {
            ship.trajectory.Update(ship.orbitParams, ship.centralBody, epoch, null);
            epoch += AllowedWarp(ship, epoch) * FrameSeconds;
            transitioned = world.Step(epoch, ship);
        }

        Assert.IsTrue(transitioned, "перемотка пронесла корабль сквозь сферу влияния");
        Assert.AreSame(mimas, ship.centralBody);
    }

    [Test]
    public void ManeuverTrajectoryEvents_DoNotSlowWarp_BurnStartDoes()
    {
        SpaceObject titan = world["titan"];
        Ship ship = world.PutShip(titan, Parking(titan), 0.0);
        ship.trajectory.Update(ship.orbitParams, titan, 0.0, null);
        Assert.AreEqual(PatchEndReason.Horizon, ship.trajectory.patches[0].EndReason);
        Assert.IsNull(ship.NextEvent(0.0), "на собственной траектории событий нет");

        const double NodeEpoch = 3600.0;
        ship.CreateManeuver(NodeEpoch);
        Maneuver maneuver = ship.GetManeuver();
        try
        {
            // Уход из сферы влияния Титана — событие на плановой траектории, не на корабельной.
            maneuver.deltaLVLHVelocity = new Vector3d(1000.0, 0.0, 0.0);
            maneuver.CalcAndDraw();
            maneuver.UpdateTrajectory(null);
            Assert.AreEqual(PatchEndReason.EscapedSOI, maneuver.trajectory.patches[0].EndReason);

            Ship.WarpEvent next = ship.NextEvent(0.0);
            Assert.IsNotNull(next);
            Assert.AreEqual("BURN START", next.Reason);
            Assert.Less(next.Epoch, NodeEpoch);
            Assert.Less(next.Epoch, maneuver.trajectory.patches[0].EndEpoch);
        }
        finally
        {
            Object.DestroyImmediate(maneuver.GameObject);
        }
    }

    static double AllowedWarp(Ship ship, double epoch)
    {
        Ship.WarpEvent next = ship.NextEvent(epoch);
        double allowed = next == null ? Requested : WarpLimit.Allowed(Requested, next.Epoch - epoch);
        return Mathd.Min(allowed, WarpLimit.FrameCap(ship.NarrowestFlybyWindow(), FrameSeconds));
    }

    static OrbitElements Parking(SpaceObject central) => new()
    {
        semiMajorAxis = 2975000.0,
        eccentricity = 0.0,
        inclination = 0.0,
        longitudeOfAscendingNode = 0.0,
        argumentOfPeriapsis = 0.0,
        meanAnomalyAtEpoch = 0.0,
        startEpoch = 0.0,
        mu = central.MU,
    };

    /// <summary>Встречный курс: в заданный момент корабль ровно на границе сферы влияния тела и идёт внутрь.</summary>
    OrbitElements InboundTo(SpaceObject body, double epoch)
    {
        (Vector3d bodyR, Vector3d bodyV) = TrajectoryPredictor.BodyStateAt(body, epoch);
        Vector3d approach = (-2.0 * bodyV).normalized;
        Vector3d side = Vector3d.Cross(approach, new Vector3d(0, 0, 1)).normalized;
        Vector3d offset = (-approach * 0.9 + side * 0.435) * body.SOI;
        return AstroDynamic.CalculateOrbitElements(bodyR + offset, -bodyV, world.saturn.MU, epoch);
    }
}
