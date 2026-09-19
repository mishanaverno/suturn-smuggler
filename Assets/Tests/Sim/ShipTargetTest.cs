using System;
using NUnit.Framework;
using OuterSpace;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Точки сближения принадлежат последнему манёвру, а без плана — траектории корабля.
/// </summary>
public class ShipTargetTest
{
    const double ShipRadius = 8.0e8;
    const double StationRadius = 8.01e8;
    const double ConjunctionEpoch = 15000.0;

    SaturnTestWorld world;
    SpaceObject station;
    Ship ship;

    [SetUp]
    public void SetUp()
    {
        world = new SaturnTestWorld();
        station = world.Put(world.saturn, Circular(StationRadius, 0.0), 0.0);
        SimMono.target = station;
        ship = world.PutShip(world.saturn, ShipOrbit(), 0.0);
    }

    [TearDown]
    public void TearDown() => world.Dispose();

    OrbitElements Circular(double radius, double meanAnomaly, bool retrograde = false) => new()
    {
        semiMajorAxis = radius,
        eccentricity = 0.0,
        inclination = retrograde ? 180.0 : 0.0,
        longitudeOfAscendingNode = 0.0,
        argumentOfPeriapsis = 0.0,
        meanAnomalyAtEpoch = meanAnomaly,
        startEpoch = 0.0,
        mu = world.saturn.MU,
    };

    /// <summary>Встречный курс: минимум расстояния приходится на ConjunctionEpoch.</summary>
    OrbitElements ShipOrbit()
    {
        double n = Math.Sqrt(world.saturn.MU / Math.Pow(ShipRadius, 3));
        return Circular(ShipRadius, -2.0 * n * ConjunctionEpoch, retrograde: true);
    }

    void UpdateShipTrajectory() =>
        ship.trajectory.Update(ship.orbitParams, ship.centralBody, 0.0, ship.TargetForTrajectory(null));

    [Test]
    public void Approaches_AreComputed_WithoutManeuver()
    {
        Assert.IsNull(ship.GetManeuver());
        UpdateShipTrajectory();

        Assert.AreSame(station, ship.Mono.Target);
        Assert.IsNotNull(ship.Mono.Approaches);
        Assert.Greater(ship.Mono.Approaches.Count, 0);
    }

    [Test]
    public void OnlyLastManeuver_HasApproaches()
    {
        UpdateShipTrajectory();
        ship.CreateManeuver(600.0);
        Maneuver first = ship.GetManeuver();
        first.deltaLVLHVelocity = new DoublePrecision.Vector3d(40.0, 0.0, 0.0);
        first.CalcAndDraw();
        ship.CreateManeuver(1200.0);
        Maneuver last = ship.GetManeuver();
        try
        {
            UpdateShipTrajectory();
            first.UpdateTrajectory(ship.TargetForTrajectory(first));
            last.UpdateTrajectory(ship.TargetForTrajectory(last));

            Assert.IsNull(ship.Mono.Approaches);
            Assert.IsNull(first.Mono.Approaches);
            Assert.Greater(last.Mono.Approaches.Count, 0);
            Assert.AreSame(station, last.Mono.Target);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(last.GameObject);
            UnityEngine.Object.DestroyImmediate(first.GameObject);
        }
    }

    [Test]
    public void DeletingLastManeuver_RestoresShipApproaches()
    {
        ship.CreateManeuver(600.0);
        GameObject maneuverObject = ship.GetManeuver().GameObject;
        UpdateShipTrajectory();
        Assert.IsNull(ship.Mono.Approaches);

        // Ship.DeleteManeuver снимает объект через Destroy — в edit-mode это ошибка в логе,
        // а не в игре; сам объект приходится убирать вручную.
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Destroy may not be called from edit mode"));
        ship.DeleteManeuver();
        UnityEngine.Object.DestroyImmediate(maneuverObject);
        UpdateShipTrajectory();

        Assert.IsNull(ship.GetManeuver());
        Assert.Greater(ship.Mono.Approaches.Count, 0);
        Assert.AreSame(station, ship.Mono.Target);
    }
}
