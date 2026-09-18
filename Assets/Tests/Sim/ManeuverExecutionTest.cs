using System;
using DoublePrecision;
using Game;
using NUnit.Framework;
using OuterSpace;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using UnityEngine;

/// <summary>
/// Исполнение манёвра: направление тяги, накопление характеристической скорости и отсечка.
/// Прожиг здесь настоящий — конечной длительности, шагами по симуляционному времени, — потому
/// что именно на длинных прожигах разъезжаются план и исполнение.
/// </summary>
public class ManeuverExecutionTest
{
    const double ShipRadius = 4.5e8;

    SaturnTestWorld world;

    [SetUp]
    public void SetUp() => world = new SaturnTestWorld();

    [TearDown]
    public void TearDown() => world.Dispose();

    OrbitElements Circular(double radius) => new()
    {
        semiMajorAxis = radius,
        eccentricity = 0.0,
        inclination = 0.0,
        longitudeOfAscendingNode = 0.0,
        argumentOfPeriapsis = 0.0,
        meanAnomalyAtEpoch = 0.0,
        startEpoch = 0.0,
        mu = world.saturn.MU,
    };

    double Period => AstroDynamic.Period(Circular(ShipRadius));

    /// <summary>Корабль с манёвром через четверть витка и тягой, растягивающей прожиг на duration.</summary>
    Ship Planned(Vector3d deltaLVLH, double duration)
    {
        Ship ship = world.PutShip(world.saturn, Circular(ShipRadius), 0.0);
        ship.orientation = ShipOrientation.Maneuver;
        ship.CreateManeuver(0.25 * Period);
        Maneuver maneuver = ship.GetManeuver();
        maneuver.deltaLVLHVelocity = deltaLVLH;
        maneuver.CalcAndDraw();
        ship.thrust = ship.mass * maneuver.PlannedMagnitude / duration;
        return ship;
    }

    /// <summary>Прожиг от from до to шагами step: как FixedUpdate в игре, но по заданной сетке.</summary>
    void Burn(Ship ship, double from, double to, double step)
    {
        world.Step(from, ship);
        ship.UpdateDirection();
        // Разворот занимает время, а проверяется здесь не он: корабль ставится уже
        // направленным по режиму, как если бы довернулся заранее.
        ship.AlignInstantly();
        ship.SetThrust(true);
        for (double epoch = from + step; epoch <= to + 0.5 * step; epoch += step)
        {
            if (!ship.Thrusting) break;
            ship.UpdateDirection();
            world.Step(epoch, ship);
        }
        ship.SetThrust(false);
    }

    /// <summary>Прожиг, центрированный на узле: половина длительности до него, половина после.</summary>
    void BurnCentered(Ship ship, double step)
    {
        double node = ship.GetManeuver().startEpoch;
        double half = 0.5 * ship.RemainingDeltaV / ship.Acceleration;
        Burn(ship, node - half, node + half, step);
    }

    static void AssertRelative(double expected, double actual, double tolerance, string what) =>
        Assert.AreEqual(expected, actual, Math.Abs(expected) * tolerance, what);

    [Test]
    public void InstantBurn_ReproducesThePlan()
    {
        const double Step = 1.0e-3;
        Ship ship = Planned(new Vector3d(80.0, 60.0, 0.0), Step);
        OrbitElements plan = ship.GetManeuver().newOrbitParams;

        BurnCentered(ship, Step);

        AssertRelative(plan.semiMajorAxis, ship.orbitParams.semiMajorAxis, 1e-6, "a");
        AssertRelative(plan.eccentricity, ship.orbitParams.eccentricity, 1e-6, "e");
        AssertRelative(plan.inclination, ship.orbitParams.inclination, 1e-6, "i");
    }

    /// <summary>
    /// Ловит возврат к пересчёту направления через LVLH: тот базис привязан к радиус-вектору
    /// и за четверть витка разворачивается почти на 90°, а направление тяги — не должно.
    /// </summary>
    [Test]
    public void ThrustDirection_StaysFixed_InTheInertialFrame()
    {
        double duration = 0.25 * Period;
        const int Steps = 64;
        double step = duration / Steps;
        Ship ship = Planned(new Vector3d(200.0, 0.0, 0.0), duration);

        double from = ship.GetManeuver().startEpoch - 0.5 * duration;
        world.Step(from, ship);
        ship.UpdateDirection();
        ship.AlignInstantly();
        ship.SetThrust(true);
        Vector3d first = ship.Direction;
        Vector3d startPosition = ship.simTransform.RELATIVE_R;

        for (int i = 1; i <= Steps && ship.Thrusting; i++)
        {
            ship.UpdateDirection();
            world.Step(from + i * step, ship);
        }
        ship.SetThrust(false);

        double turned = Vector3d.Angle(startPosition, ship.simTransform.RELATIVE_R);
        Assert.Greater(turned, 45.0, "за прожиг корабль должен уйти по орбите заметно далеко");
        Assert.Less(Vector3d.Angle(first, ship.Direction) * Math.PI / 180.0, 1e-9);
    }

    [Test]
    public void PlannedDeltaV_SurvivesSphereOfInfluenceChange()
    {
        Ship ship = world.PutShip(world.saturn, Circular(ShipRadius), 0.0);
        ship.CreateManeuver(1000.0);
        Maneuver maneuver = ship.GetManeuver();
        maneuver.deltaLVLHVelocity = new Vector3d(120.0, 45.0, -30.0);
        maneuver.CalcAndDraw();
        Vector3d before = maneuver.PlannedDeltaV;

        SpaceObject previous = ship.centralBody;
        ship.SetCentralBody(world["titan"]);
        maneuver.Reframe(previous);

        Assert.AreEqual(before.x, maneuver.PlannedDeltaV.x);
        Assert.AreEqual(before.y, maneuver.PlannedDeltaV.y);
        Assert.AreEqual(before.z, maneuver.PlannedDeltaV.z);
    }

    [Test]
    public void Cutoff_BurnsExactlyThePlannedTotal()
    {
        const double Duration = 4000.0;
        const double Step = 20.0;
        Ship ship = Planned(new Vector3d(180.0, 0.0, 0.0), Duration);
        double planned = ship.GetManeuver().PlannedMagnitude;

        BurnCentered(ship, Step);

        Assert.IsFalse(ship.Thrusting, "двигатель должен выключиться сам");
        Assert.AreEqual(planned, ship.BurnedDeltaV, ship.Acceleration * Step);
    }

    [Test]
    public void Cutoff_DoesNotOvershoot_MoreThanOneStep()
    {
        const double Duration = 4000.0;
        const double Step = 20.0;
        Ship ship = Planned(new Vector3d(180.0, 0.0, 0.0), Duration);

        BurnCentered(ship, Step);

        Assert.LessOrEqual(ship.RemainingDeltaV, 0.0);
        Assert.GreaterOrEqual(ship.RemainingDeltaV, -ship.Acceleration * Step);
    }

    [Test]
    public void Result_DoesNotDependOnThePhysicsStep()
    {
        const double Duration = 4000.0;
        Ship coarse = Planned(new Vector3d(180.0, 40.0, 0.0), Duration);
        Ship fine = Planned(new Vector3d(180.0, 40.0, 0.0), Duration);

        BurnCentered(coarse, 20.0);
        BurnCentered(fine, 5.0);

        Assert.AreEqual(coarse.BurnedDeltaV, fine.BurnedDeltaV, coarse.Acceleration * 20.0);
        // Погрешность положения между шагами — первого порядка по шагу, и на вчетверо
        // меньшем шаге она вчетверо меньше; сама орбита от этого сдвигается на доли процента.
        AssertRelative(coarse.orbitParams.semiMajorAxis, fine.orbitParams.semiMajorAxis, 1e-3, "a");
        AssertRelative(coarse.orbitParams.eccentricity, fine.orbitParams.eccentricity, 1e-2, "e");
    }

    [Test]
    public void Warp_IsHeldAtRealTime_WhileTheEngineRuns()
    {
        Ship ship = Planned(new Vector3d(180.0, 0.0, 0.0), 4000.0);
        TimeToggler toggler = GameMono.instance.TimeToggler;
        toggler.Shift(6);
        Assert.Greater(toggler.Current, 1u);

        ship.SetThrust(true);
        Assert.AreEqual(1u, toggler.Current);
        Assert.AreEqual(1u, toggler.Faster().Faster().Current);

        ship.SetThrust(false);
        Assert.Greater(toggler.Faster().Current, 1u);
    }

    [Test]
    public void Acceleration_ScalesWithMass()
    {
        const double Duration = 4000.0;
        const double Step = 20.0;
        Ship light = Planned(new Vector3d(180.0, 0.0, 0.0), Duration);
        Ship heavy = Planned(new Vector3d(180.0, 0.0, 0.0), Duration);
        heavy.mass = 2.0 * light.mass;
        heavy.thrust = light.thrust;

        double node = light.GetManeuver().startEpoch;
        Burn(light, node, node + 1000.0, Step);
        Burn(heavy, node, node + 1000.0, Step);

        Assert.AreEqual(0.5 * light.BurnedDeltaV, heavy.BurnedDeltaV, 1e-9);
    }

    /// <summary>
    /// Соглашение части 4: прожиг центрируется на узле. Начатый в момент узла, он весь
    /// приходится на время после него и уводит орбиту тем сильнее, чем длиннее.
    /// </summary>
    [Test]
    public void CenteredBurn_LandsCloserToThePlan_ThanOneStartedAtTheNode()
    {
        const double Duration = 20000.0;
        const double Step = 20.0;
        Ship centered = Planned(new Vector3d(300.0, 0.0, 0.0), Duration);
        Ship late = Planned(new Vector3d(300.0, 0.0, 0.0), Duration);

        BurnCentered(centered, Step);
        double node = late.GetManeuver().startEpoch;
        Burn(late, node, node + 2.0 * Duration, Step);

        Assert.Less(ApsisError(centered), ApsisError(late));
    }

    static double ApsisError(Ship ship)
    {
        (double periapsis, double apoapsis) = AstroDynamic.GetPeriapsisAndApoapsis(ship.orbitParams);
        (double plannedPeriapsis, double plannedApoapsis) =
            AstroDynamic.GetPeriapsisAndApoapsis(ship.GetManeuver().newOrbitParams);
        return Math.Abs(periapsis - plannedPeriapsis) + Math.Abs(apoapsis - plannedApoapsis);
    }

    /// <summary>
    /// Нормальная составляющая — это наклонение. Большую полуось она тоже трогает: длина
    /// вектора скорости растёт, а с ней и энергия. Но на порядки слабее, чем тангенциальная
    /// того же размера, и проверять надо именно это соотношение.
    /// </summary>
    [Test]
    public void NormalComponent_ChangesInclination_NotTheSemiMajorAxis()
    {
        const double Dv = 200.0;
        Ship ship = world.PutShip(world.saturn, Circular(ShipRadius), 0.0);
        ship.CreateManeuver(1000.0);
        Maneuver maneuver = ship.GetManeuver();
        OrbitElements flat = maneuver.newOrbitParams;

        maneuver.deltaLVLHVelocity = new Vector3d(Dv, 0.0, 0.0);
        maneuver.CalcAndDraw();
        double progradeShift = Math.Abs(maneuver.newOrbitParams.semiMajorAxis - flat.semiMajorAxis);
        Assert.AreEqual(flat.inclination, maneuver.newOrbitParams.inclination, 1e-9);

        maneuver.deltaLVLHVelocity = new Vector3d(0.0, Dv, 0.0);
        maneuver.CalcAndDraw();
        double normalShift = Math.Abs(maneuver.newOrbitParams.semiMajorAxis - flat.semiMajorAxis);

        Assert.Greater(maneuver.newOrbitParams.inclination, flat.inclination + 0.5);
        Assert.Less(normalShift, 0.05 * progradeShift);
    }
}
