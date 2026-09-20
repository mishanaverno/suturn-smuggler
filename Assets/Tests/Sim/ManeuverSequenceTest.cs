using System;
using DoublePrecision;
using NUnit.Framework;
using OuterSpace;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Последовательный план: каждый новый узел строится на результате предыдущего.</summary>
public class ManeuverSequenceTest
{
    const double ShipRadius = 4.5e8;

    SaturnTestWorld world;

    [SetUp]
    public void SetUp() => world = new SaturnTestWorld();

    [TearDown]
    public void TearDown() => world.Dispose();

    OrbitElements Circular() => new()
    {
        semiMajorAxis = ShipRadius,
        eccentricity = 0.0,
        inclination = 0.0,
        longitudeOfAscendingNode = 0.0,
        argumentOfPeriapsis = 0.0,
        meanAnomalyAtEpoch = 0.0,
        startEpoch = 0.0,
        mu = world.saturn.MU,
    };

    [Test]
    public void NewManeuver_UsesPreviousManeuverTrajectory()
    {
        Ship ship = world.PutShip(world.saturn, Circular(), 0.0);
        ship.CreateManeuver(1000.0);
        Maneuver first = ship.GetManeuver();
        first.deltaLVLHVelocity = new Vector3d(1200.0, 0.0, 0.0);
        first.CalcAndDraw();

        ship.CreateManeuver(5000.0);
        Maneuver second = ship.GetManeuver();

        Assert.AreSame(first, second.Previous);
        Assert.AreSame(first.CentralBody, second.CentralBody);
        Assert.AreEqual(first.newOrbitParams.semiMajorAxis, second.newOrbitParams.semiMajorAxis,
            Math.Abs(first.newOrbitParams.semiMajorAxis) * 1e-10);
        Assert.AreEqual(first.newOrbitParams.eccentricity, second.newOrbitParams.eccentricity, 1e-10);
        Assert.AreNotEqual(Circular().semiMajorAxis, second.newOrbitParams.semiMajorAxis);

        UnityEngine.Object.DestroyImmediate(second.GameObject);
        UnityEngine.Object.DestroyImmediate(first.GameObject);
    }

    [Test]
    public void EditingPreviousManeuver_RecalculatesFollowingManeuver()
    {
        Ship ship = world.PutShip(world.saturn, Circular(), 0.0);
        ship.CreateManeuver(1000.0);
        Maneuver first = ship.GetManeuver();
        ship.CreateManeuver(5000.0);
        Maneuver second = ship.GetManeuver();
        double before = second.newOrbitParams.semiMajorAxis;

        first.deltaLVLHVelocity = new Vector3d(1200.0, 0.0, 0.0);
        first.CalcAndDraw();

        Assert.AreNotEqual(before, second.newOrbitParams.semiMajorAxis);
        Assert.AreEqual(first.newOrbitParams.semiMajorAxis, second.newOrbitParams.semiMajorAxis,
            Math.Abs(first.newOrbitParams.semiMajorAxis) * 1e-10);

        UnityEngine.Object.DestroyImmediate(second.GameObject);
        UnityEngine.Object.DestroyImmediate(first.GameObject);
    }

    [Test]
    public void DeleteManeuver_SelectsPreviousManeuver()
    {
        Ship ship = world.PutShip(world.saturn, Circular(), 0.0);
        ship.CreateManeuver(1000.0);
        Maneuver first = ship.GetManeuver();
        ship.CreateManeuver(5000.0);
        Maneuver second = ship.GetManeuver();

        LogAssert.Expect(LogType.Error,
            new System.Text.RegularExpressions.Regex("Destroy may not be called from edit mode"));
        ship.DeleteManeuver();

        Assert.AreSame(first, ship.GetManeuver());
        UnityEngine.Object.DestroyImmediate(second.GameObject);
        UnityEngine.Object.DestroyImmediate(first.GameObject);
    }

    [Test]
    public void DeleteNextManeuver_PromotesFollowingManeuver()
    {
        Ship ship = world.PutShip(world.saturn, Circular(), 0.0);
        ship.CreateManeuver(1000.0);
        Maneuver first = ship.GetManeuver();
        first.deltaLVLHVelocity = new Vector3d(1200.0, 0.0, 0.0);
        first.CalcAndDraw();
        ship.CreateManeuver(5000.0);
        Maneuver second = ship.GetManeuver();

        LogAssert.Expect(LogType.Error,
            new System.Text.RegularExpressions.Regex("Destroy may not be called from edit mode"));
        ship.DeleteNextManeuver();

        Assert.AreSame(second, ship.GetManeuver());
        Assert.AreSame(second, ship.GetNextManeuver());
        Assert.IsNull(second.Previous);
        Assert.AreEqual(ship.orbitParams.semiMajorAxis, second.SourceOrbit.semiMajorAxis, 1e-9);
        UnityEngine.Object.DestroyImmediate(first.GameObject);
        UnityEngine.Object.DestroyImmediate(second.GameObject);
    }

    [Test]
    public void CannotCreateMoreThanThreeManeuvers()
    {
        Ship ship = world.PutShip(world.saturn, Circular(), 0.0);
        ship.CreateManeuver(1000.0);
        Maneuver first = ship.GetManeuver();
        ship.CreateManeuver(2000.0);
        Maneuver second = ship.GetManeuver();
        ship.CreateManeuver(3000.0);
        Maneuver third = ship.GetManeuver();

        Assert.AreEqual(Ship.MaxManeuvers, ship.ManeuverCount);
        Assert.IsFalse(ship.CanCreateManeuver);
        ship.CreateManeuver(4000.0);
        Assert.AreSame(third, ship.GetManeuver());
        Assert.AreEqual(Ship.MaxManeuvers, ship.ManeuverCount);

        LogAssert.Expect(LogType.Error,
            new System.Text.RegularExpressions.Regex("Destroy may not be called from edit mode"));
        ship.DeleteManeuver();
        Assert.IsTrue(ship.CanCreateManeuver);
        ship.CreateManeuver(4000.0);
        Maneuver replacement = ship.GetManeuver();
        Assert.AreEqual(Ship.MaxManeuvers, ship.ManeuverCount);

        UnityEngine.Object.DestroyImmediate(third.GameObject);
        UnityEngine.Object.DestroyImmediate(replacement.GameObject);
        UnityEngine.Object.DestroyImmediate(second.GameObject);
        UnityEngine.Object.DestroyImmediate(first.GameObject);
    }

    [Test]
    public void Maneuvers_HaveSequenceIndicesForThreeDisplayColors()
    {
        Ship ship = world.PutShip(world.saturn, Circular(), 0.0);
        ship.CreateManeuver(1000.0);
        Maneuver first = ship.GetManeuver();
        ship.CreateManeuver(2000.0);
        Maneuver second = ship.GetManeuver();
        ship.CreateManeuver(3000.0);
        Maneuver third = ship.GetManeuver();

        Assert.AreEqual(0, first.SequenceIndex);
        Assert.AreEqual(1, second.SequenceIndex);
        Assert.AreEqual(2, third.SequenceIndex);

        LogAssert.Expect(LogType.Error,
            new System.Text.RegularExpressions.Regex("Destroy may not be called from edit mode"));
        ship.DeleteNextManeuver();
        Assert.AreEqual(0, second.SequenceIndex);
        Assert.AreEqual(1, third.SequenceIndex);

        UnityEngine.Object.DestroyImmediate(first.GameObject);
        UnityEngine.Object.DestroyImmediate(third.GameObject);
        UnityEngine.Object.DestroyImmediate(second.GameObject);
    }

    [Test]
    public void NewManeuver_CannotPrecedePreviousManeuver()
    {
        Ship ship = world.PutShip(world.saturn, Circular(), 0.0);
        ship.CreateManeuver(5000.0);
        Maneuver first = ship.GetManeuver();

        ship.CreateManeuver(0.0);
        Maneuver second = ship.GetManeuver();

        Assert.AreEqual(first.startEpoch, second.startEpoch);
        UnityEngine.Object.DestroyImmediate(second.GameObject);
        UnityEngine.Object.DestroyImmediate(first.GameObject);
    }

    [Test]
    public void TimeStep_UsesOrbitOfTheActiveManeuver()
    {
        Ship ship = world.PutShip(world.saturn, Circular(), 0.0);
        ship.CreateManeuver(1000.0);
        Maneuver first = ship.GetManeuver();
        first.deltaLVLHVelocity = new Vector3d(1200.0, 0.0, 0.0);
        first.CalcAndDraw();
        ship.CreateManeuver(5000.0);
        Maneuver second = ship.GetManeuver();

        double expected = Ship.TimeStep(AstroDynamic.Period(second.SourceOrbit), ship.stepScale);
        double oldShipOrbitStep = Ship.TimeStep(AstroDynamic.Period(ship.orbitParams), ship.stepScale);

        Assert.AreEqual(expected, ship.CurrentTimeStep, expected * 1e-12);
        Assert.AreNotEqual(oldShipOrbitStep, ship.CurrentTimeStep);
        UnityEngine.Object.DestroyImmediate(second.GameObject);
        UnityEngine.Object.DestroyImmediate(first.GameObject);
    }

    [Test]
    public void TimeStep_DoesNotShrinkAlongOpenTrajectory()
    {
        Ship ship = world.PutShip(world.saturn, Circular(), 0.0);
        ship.CreateManeuver(1000.0);
        Maneuver first = ship.GetManeuver();
        first.deltaLVLHVelocity = new Vector3d(6000.0, 0.0, 0.0);
        first.CalcAndDraw();
        Assert.GreaterOrEqual(first.newOrbitParams.eccentricity, 1.0);

        ship.CreateManeuver(5000.0);
        Maneuver second = ship.GetManeuver();
        double initialStep = ship.CurrentTimeStep;

        for (int i = 0; i < 100; i++) ship.ShiftManeuverTime(initialStep);

        Assert.AreEqual(initialStep, ship.CurrentTimeStep, initialStep * 1e-12);
        Assert.AreEqual(5000.0 + 100.0 * initialStep, second.startEpoch, 1e-9);
        Assert.Greater(second.startEpoch, first.startEpoch + first.trajectory.settings.openOrbitHorizon,
            "узел должен суметь пересечь конец рассчитанной открытой дуги");
        UnityEngine.Object.DestroyImmediate(second.GameObject);
        UnityEngine.Object.DestroyImmediate(first.GameObject);
    }

    [Test]
    public void AutomationAndCutoff_UseNearestManeuver()
    {
        Ship ship = world.PutShip(world.saturn, Circular(), 0.0);
        ship.CreateManeuver(1000.0);
        Maneuver first = ship.GetManeuver();
        first.deltaLVLHVelocity = new Vector3d(100.0, 0.0, 0.0);
        first.CalcAndDraw();

        ship.CreateManeuver(5000.0);
        Maneuver last = ship.GetManeuver();
        last.deltaLVLHVelocity = new Vector3d(500.0, 0.0, 0.0);
        last.CalcAndDraw();

        Assert.AreSame(last, ship.GetManeuver(), "ручки должны редактировать последний узел");
        Assert.AreSame(first, ship.GetNextManeuver(), "автоматика должна исполнять ближайший узел");
        Assert.AreEqual(first.PlannedMagnitude, ship.RemainingDeltaV, 1e-9);
        Assert.AreEqual(first.startEpoch - 0.5 * first.PlannedMagnitude / ship.Acceleration,
            ship.BurnStartEpoch, 1e-9);

        ship.orientation = ShipOrientation.Maneuver;
        ship.UpdateDirection();
        Assert.Less(Vector3d.Angle(first.PlannedDeltaV, ship.CommandedDirection), 1e-5);
        ship.AlignInstantly();
        ship.thrust = ship.mass * 10.0;
        ship.SetThrust(true);
        world.Step(11.0, ship);

        Assert.IsFalse(ship.Thrusting, "отсечка должна сработать по Δv ближайшего узла");
        Assert.GreaterOrEqual(ship.BurnedDeltaV, first.PlannedMagnitude);
        Assert.LessOrEqual(ship.BurnedDeltaV, first.PlannedMagnitude + 10.0);
        Assert.Less(ship.BurnedDeltaV, last.PlannedMagnitude);
        UnityEngine.Object.DestroyImmediate(last.GameObject);
        UnityEngine.Object.DestroyImmediate(first.GameObject);
    }
}
