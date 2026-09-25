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
        // По Циолковскому: корабль легчает, и тяга под постоянное ускорение дала бы перелёт.
        Engine engine = ship.engine.spec.nuclear;
        double burned = ship.Mass * (1.0 - Math.Exp(-maneuver.PlannedMagnitude / engine.ExhaustVelocity));
        engine.thrust = engine.ExhaustVelocity * burned / duration;
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
        double start = ship.BurnStartEpoch;
        Burn(ship, start, start + ship.RemainingBurnDuration, step);
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

        // Окно прожига точно по Циолковскому: остаток доходит до нуля с точностью округления.
        Assert.LessOrEqual(ship.RemainingDeltaV, 1e-9);
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
    public void BurnedDeltaV_FollowsTsiolkovsky()
    {
        const double Step = 20.0;
        Ship light = Planned(new Vector3d(180.0, 0.0, 0.0), 4000.0);
        Ship heavy = Planned(new Vector3d(180.0, 0.0, 0.0), 4000.0);
        heavy.engine.spec.nuclear.thrust = light.engine.spec.nuclear.thrust;
        heavy.dryMass += light.Mass;
        double lightBefore = light.Mass;
        double heavyBefore = heavy.Mass;

        double node = light.GetManeuver().startEpoch;
        Burn(light, node, node + 1000.0, Step);
        Burn(heavy, node, node + 1000.0, Step);

        double exhaust = light.engine.Engine.ExhaustVelocity;
        Assert.AreEqual(exhaust * Math.Log(lightBefore / light.Mass), light.BurnedDeltaV, 1e-9);
        Assert.AreEqual(exhaust * Math.Log(heavyBefore / heavy.Mass), heavy.BurnedDeltaV, 1e-9);
        Assert.AreEqual(lightBefore - light.Mass, heavyBefore - heavy.Mass, 1e-9, "расход от массы не зависит");
        Assert.Less(heavy.BurnedDeltaV, 0.51 * light.BurnedDeltaV);
    }

    [Test]
    public void EngineFlamesOut_WhenMethaneRunsOut()
    {
        const double Step = 1.0;
        Ship ship = Planned(new Vector3d(10000.0, 0.0, 0.0), 100.0);
        ship.tanks.methane = 50.0;
        double loxBefore = ship.tanks.lox;
        double expected = ship.engine.Engine.ExhaustVelocity * Math.Log(ship.Mass / (ship.Mass - 50.0));

        double node = ship.GetManeuver().startEpoch;
        Burn(ship, node, node + 1000.0, Step);

        Assert.AreEqual(0.0, ship.tanks.methane);
        Assert.AreEqual(loxBefore, ship.tanks.lox, "ЯРД кислород не тратит");
        Assert.AreEqual(expected, ship.BurnedDeltaV, 1e-9);
        Assert.AreEqual(0.0, ship.engine.AvailableDeltaV(ship.Mass));
    }

    [Test]
    public void Prox_SpendsMethaneAndLoxByRatio_AndCruiseNeedsNoLox()
    {
        Ship ship = Planned(new Vector3d(10000.0, 0.0, 0.0), 100.0);
        ship.SetEngineMode(EngineMode.Prox);
        ship.engine.SetLoxThrottle(1.0);
        ship.tanks.lox = 35.0;
        double methaneBefore = ship.tanks.methane;

        double node = ship.GetManeuver().startEpoch;
        Burn(ship, node, node + 1000.0, 1.0);

        Assert.AreEqual(0.0, ship.tanks.lox, 1e-12);
        Assert.AreEqual(10.0, methaneBefore - ship.tanks.methane, 1e-9, "O/F 3.5 : 1");

        ship.tanks.lox = 0.0;
        ship.SetThrust(true);
        Assert.IsFalse(ship.Thrusting, "без кислорода ЖРД не зажигается");
        ship.SetEngineMode(EngineMode.Cruise);
        ship.engine.SetLoxThrottle(0.0);
        ship.SetThrust(true);
        Assert.IsTrue(ship.Thrusting, "ЯРД без форсажа кислород не нужен");
        ship.SetThrust(false);
    }

    [Test]
    public void Throttles_PushEachOther_AndLatchInProxOnContact()
    {
        Ship ship = world.PutShip(world.saturn, Circular(ShipRadius), 0.0);
        ship.engine.SetMainThrottle(0.5);
        ship.engine.SetLoxThrottle(0.7);
        Assert.AreEqual(0.7, ship.engine.MainThrottle, "LOX выше MAIN толкает MAIN");
        ship.engine.SetMainThrottle(0.3);
        Assert.AreEqual(0.3, ship.engine.LoxThrottle, "MAIN ниже LOX толкает LOX");
        ship.engine.SetLoxThrottle(0.1);
        Assert.AreEqual(0.3, ship.engine.MainThrottle, "в CRUISE не сцеплены: LOX уходит вниз один");

        ship.SetEngineMode(EngineMode.Prox);
        ship.engine.SetMainThrottle(0.6);
        Assert.AreEqual(0.1, ship.engine.LoxThrottle, "не коснулись — не сцепились");

        ship.engine.SetLoxThrottle(0.6);
        ship.engine.SetLoxThrottle(0.2);
        Assert.AreEqual(0.2, ship.engine.MainThrottle, "коснулись в PROX — ходят вместе и вниз");

        ship.SetEngineMode(EngineMode.Cruise);
        ship.engine.SetLoxThrottle(0.0);
        Assert.AreEqual(0.2, ship.engine.MainThrottle, "вне PROX сцепка снята");
    }

    [Test]
    public void Prox_FlamesOut_WhenEnteredWithSplitThrottles()
    {
        Ship ship = Planned(new Vector3d(100.0, 0.0, 0.0), 100.0);
        ship.engine.SetLoxThrottle(0.5);
        ship.SetThrust(true);
        Assert.IsTrue(ship.Thrusting);

        ship.SetEngineMode(EngineMode.Prox);
        Assert.IsFalse(ship.Thrusting, "рукоятки разведены — смесь не та");
        ship.SetThrust(true);
        Assert.IsFalse(ship.Thrusting, "пока не сцеплены, не зажигается");

        ship.engine.SetLoxThrottle(1.0);
        ship.SetThrust(true);
        Assert.IsTrue(ship.Thrusting, "LOX дошёл до MAIN и сцепился");
        ship.SetThrust(false);
    }

    [Test]
    public void Idle_DoesNotIgnite_AndShutsDownOnSwitch()
    {
        Ship ship = world.PutShip(world.saturn, Circular(ShipRadius), 0.0);
        ship.SetThrust(true);
        Assert.IsTrue(ship.Thrusting);

        ship.SetEngineMode(EngineMode.Idle);
        Assert.IsFalse(ship.Thrusting, "IDLE глушит двигатель");
        ship.SetThrust(true);
        Assert.IsFalse(ship.Thrusting, "в IDLE зажигание не включается");
    }

    [Test]
    public void Afterburner_BlendsBetweenPureAndFullLox()
    {
        Ship ship = world.PutShip(world.saturn, Circular(ShipRadius), 0.0);
        Propulsion p = ship.engine.spec;

        ship.engine.SetLoxThrottle(1.0);
        Assert.AreEqual(p.nuclearLox.thrust, ship.engine.Engine.thrust, 1e-6);
        Assert.AreEqual(p.nuclearLox.isp, ship.engine.Engine.isp, 1e-9);

        ship.engine.SetMainThrottle(0.8);
        ship.engine.SetLoxThrottle(0.4);
        Assert.AreEqual(0.5 * (p.nuclear.thrust + p.nuclearLox.thrust), ship.engine.Engine.thrust, 1e-6,
            "форсаж — доля LOX от MAIN");
        Assert.AreEqual(0.5, ship.engine.Engine.oxidizerRatio, 1e-12);
    }

    [Test]
    public void Reactor_SpoolsUpInTenSeconds_AndLimitsThrust()
    {
        Ship ship = Planned(new Vector3d(10000.0, 0.0, 0.0), 10000.0);
        double node = ship.GetManeuver().startEpoch;
        world.Step(node, ship);
        ship.engine.reactorPower = 0.0;

        ship.SetThrust(true);
        world.Step(node + 5.0, ship);
        Assert.AreEqual(0.5, ship.engine.reactorPower, 1e-9);
        Assert.AreEqual(ship.engine.spec.nuclear.isp * Math.Sqrt(15000.0 / 25000.0), ship.engine.SpecificImpulse, 1e-9,
            "импульс ниже паспортного, пока реактор не догнал заказ");
        Assert.AreEqual(15000.0, ship.engine.ReactorTemperature, 1e-6);
        world.Step(node + 10.0, ship);
        Assert.AreEqual(1.0, ship.engine.reactorPower, 1e-12);

        ship.SetEngineMode(EngineMode.Prox);
        world.Step(node + 13.0, ship);
        Assert.AreEqual(0.7, ship.engine.reactorPower, 1e-9, "в PROX реактор остывает");
        ship.SetThrust(false);
    }

    [Test]
    public void ColdStart_CostsMorePropellant_ThanWarmReactor()
    {
        Ship warm = Planned(new Vector3d(10000.0, 0.0, 0.0), 10000.0);
        Ship cold = Planned(new Vector3d(10000.0, 0.0, 0.0), 10000.0);
        double node = warm.GetManeuver().startEpoch;
        world.Step(node, cold);
        cold.engine.reactorPower = 0.0;
        double warmBefore = warm.Mass;
        double coldBefore = cold.Mass;

        Burn(warm, node, node + 20.0, 0.1);
        Burn(cold, node, node + 20.0, 0.1);

        double exhaust = warm.engine.Engine.ExhaustVelocity;
        double warmPerKg = warm.BurnedDeltaV / (warmBefore - warm.Mass);
        double coldPerKg = cold.BurnedDeltaV / (coldBefore - cold.Mass);
        Assert.AreEqual(exhaust * Math.Log(warmBefore / warm.Mass), warm.BurnedDeltaV, 1e-9, "прогретый — по паспорту");
        Assert.Less(cold.BurnedDeltaV, warm.BurnedDeltaV, "холодный не успел разогнаться");
        Assert.Less(coldPerKg, warmPerKg, "разогрев стоит топлива");
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
