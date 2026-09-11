using System;
using System.Collections.Generic;
using NUnit.Framework;
using OuterSpace;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Цель принадлежит кораблю, а не манёвру: манёвр после исполнения удаляется, а показания
/// сближения нужнее всего как раз на финальных коррекциях, когда его уже нет.
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
        ship.trajectory.Update(ship.orbitParams, ship.centralBody, 0.0, SimMono.target);

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
    public void ShipAndManeuver_KeepIndependentApproaches()
    {
        UpdateShipTrajectory();
        IReadOnlyList<CloseApproach> shipApproaches = ship.trajectory.approaches;

        ship.CreateManeuver(600.0);
        Maneuver maneuver = ship.GetManeuver();
        try
        {
            maneuver.deltaLVLHVelocity = new DoublePrecision.Vector3d(40.0, 0.0, 0.0);
            maneuver.CalcAndDraw();
            maneuver.UpdateTrajectory(station);

            Assert.Greater(maneuver.trajectory.approaches.Count, 0);
            Assert.AreNotSame(shipApproaches, maneuver.trajectory.approaches);
            // План и то, что произойдёт на самом деле: разница между парами меток и есть информация.
            Assert.AreNotEqual(shipApproaches[0].Epoch, maneuver.trajectory.approaches[0].Epoch);
            Assert.AreSame(shipApproaches, ship.trajectory.approaches);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(maneuver.GameObject);
        }
    }

    [Test]
    public void DeletingManeuver_KeepsShipApproaches()
    {
        ship.CreateManeuver(600.0);
        GameObject maneuverObject = ship.GetManeuver().GameObject;
        UpdateShipTrajectory();
        Assert.Greater(ship.trajectory.approaches.Count, 0);

        // Ship.DeleteManeuver снимает объект через Destroy — в edit-mode это ошибка в логе,
        // а не в игре; сам объект приходится убирать вручную.
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Destroy may not be called from edit mode"));
        ship.DeleteManeuver();
        UnityEngine.Object.DestroyImmediate(maneuverObject);
        UpdateShipTrajectory();

        Assert.IsNull(ship.GetManeuver());
        Assert.Greater(ship.trajectory.approaches.Count, 0);
        Assert.AreSame(station, ship.trajectory.target);
    }
}
