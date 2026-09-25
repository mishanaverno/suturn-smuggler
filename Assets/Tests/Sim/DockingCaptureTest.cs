using System;
using System.Collections.Generic;
using DoublePrecision;
using Game;
using NUnit.Framework;
using OuterSpace;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using UnityEngine;

/// <summary>
/// Захват узлом: только когда в допуске всё сразу, после захвата корабль идёт со станцией
/// как одно целое, отпущенный — отходит и обратно не защёлкивается.
/// </summary>
public class DockingCaptureTest
{
    const double Radius = 2.975e6;
    const double Tick = 0.02;

    SaturnTestWorld world;
    GameObject prefab;
    Station station;
    Ship ship;

    [SetUp]
    public void SetUp()
    {
        world = new SaturnTestWorld();
        prefab = new GameObject("StationPrefab");
        station = new Station(
            new PortData { position = new Vector3d(0, -20, 0), axis = new Vector3d(0, -1, 0), up = new Vector3d(0, 0, 1) },
            new BeaconData { range = 2000, cone = 15 },
            new CaptureData { range = 0.5, lateral = 0.3, speed = 0.3, roll = 3, pitch = 3, yaw = 3 },
            new List<string>(), prefab);
        SpaceObject titan = world["titan"];
        station.SetCentralBody(titan);
        station.SetOrbit(Circular(titan));
        SimMono.target = station;

        ship = world.PutShip(titan, Circular(titan), 0.0);
        ship.leftPort = new PortData { position = new Vector3d(0, 2.5, 0), axis = new Vector3d(0, 1, 0), up = new Vector3d(0, 0, 1) };
        ship.rightPort = new PortData { position = new Vector3d(0, -2.5, 0), axis = new Vector3d(0, -1, 0), up = new Vector3d(0, 0, 1) };
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(station.GameObject);
        UnityEngine.Object.DestroyImmediate(prefab);
        world.Dispose();
    }

    OrbitElements Circular(SpaceObject central) => new()
    {
        semiMajorAxis = Radius,
        startEpoch = 0.0,
        mu = central.MU,
    };

    /// <summary>
    /// Узел корабля в range метрах перед узлом станции со сдвигом вбок, идёт к нему с
    /// closing м/с, повёрнут на roll градусов вокруг оси узла.
    /// </summary>
    void Approach(double range, double lateral, double closing, double roll)
    {
        ship.attitude.rotation = Quaterniond.AngleAxis(roll, station.port.axis) * DockingGeometry.Aligned(ship.ActivePort, station.port);
        Vector3d portAt = station.simTransform.GLOBAL_R + station.port.position + station.port.axis * range + station.port.up * lateral;
        Vector3d r = portAt - ship.attitude.rotation * ship.ActivePort.position;
        ship.simTransform.SetRELATIVE_R(r - ship.centralBody.simTransform.GLOBAL_R);
        ship.SetVelocity(station.simTransform.GLOBAL_V - station.port.axis * closing - ship.centralBody.simTransform.GLOBAL_V);
    }

    double Step(double epoch)
    {
        world.Step(epoch, ship, station);
        return epoch;
    }

    [Test]
    public void InsideAllTolerances_Captures()
    {
        Approach(0.3, 0.1, 0.1, 1.0);
        Step(Tick);
        Assert.AreSame(station, ship.DockedTo);
    }

    [TestCase(0.3, 0.4, 0.1, 1.0, TestName = "Lateral")]
    [TestCase(0.3, 0.1, 0.5, 1.0, TestName = "Speed")]
    [TestCase(0.3, 0.1, 0.1, 4.0, TestName = "Roll")]
    public void OneToleranceExceeded_DoesNotCapture(double range, double lateral, double closing, double roll)
    {
        Approach(range, lateral, closing, roll);
        Step(Tick);
        Assert.IsNull(ship.DockedTo);
    }

    [Test]
    public void Docked_StaysInPort_OverOrbit()
    {
        Approach(0.3, 0.1, 0.1, 1.0);
        double epoch = Step(Tick);
        Assert.AreSame(station, ship.DockedTo);

        double period = 2.0 * Math.PI * Math.Sqrt(Radius * Radius * Radius / station.centralBody.MU);
        double worst = 0.0;
        while (epoch < period)
        {
            epoch = Step(epoch + 60.0);
            DockingState state = ship.Docking.Value;
            worst = Math.Max(worst, Math.Max(state.Distance, 0.0));
        }
        Assert.Less(worst, 1e-3, "узел корабля ушёл из узла станции, м");
        Assert.AreSame(station, ship.DockedTo);
    }

    /// <summary>
    /// Расстыковка приходит с пульта между тиками, когда эпоха уже ушла вперёд от последнего
    /// тика. Корабль не должен при этом прыгнуть на путь, пройденный станцией за долю кадра.
    /// </summary>
    [Test]
    public void UndockBetweenTicks_DoesNotJump()
    {
        Approach(0.3, 0.1, 0.1, 1.0);
        double epoch = Step(Tick);
        world.Epoch = epoch + 0.5 * Tick;
        ship.Undock();
        Step(epoch + Tick);
        Assert.Less(ship.Docking.Value.Distance, 0.01);
    }

    [Test]
    public void Undock_PushesAway_WithoutRecapture()
    {
        Approach(0.3, 0.1, 0.1, 1.0);
        double epoch = Step(Tick);
        ship.Undock();
        for (int i = 0; i < 500; i++) epoch = Step(epoch + Tick);
        Assert.IsNull(ship.DockedTo);
        Assert.AreEqual(Ship.UndockSpeed * 10.0, ship.Docking.Value.Range, 0.05);
    }
}
