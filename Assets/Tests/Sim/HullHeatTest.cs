using NUnit.Framework;
using OuterSpace.Sim;
using OuterSpace.Sim.Systems;

public class HullHeatTest
{
    static PropulsionUnit Engine(EngineMode mode, double heat)
    {
        Propulsion spec = new()
        {
            reactor = new Reactor { idleTemperature = 5000.0, fullTemperature = 25000.0, spoolTime = 10.0, heatTime = 20.0, cooling = 0.5 },
        };
        PropulsionUnit engine = new(spec, new Tanks()) { reactorHeat = heat };
        engine.SetMode(mode);
        return engine;
    }

    static Hull HotHull() => new()
    {
        spaceTemperature = 90.0,
        temperature = 90.0,
        heatGain = 200.0,
        coolTime = 600.0,
        overheatLimit = 350.0,
        overheatRate = 1e-6,
    };

    [Test]
    public void CoolingReactor_GivesAllItsHeatAway()
    {
        // Мощность на нуле, θ остывает от 1 до 0: собственным охлаждением уходит k·∫θ = τ.
        PropulsionUnit engine = Engine(EngineMode.Prox, 1.0);
        engine.UpdateReactor(0.0, false);
        engine.UpdateReactor(10000.0, false);

        Assert.AreEqual(20.0, engine.ExcessHeat, 1e-9);
    }

    [Test]
    public void ReactorAtIdleTemperature_GivesNoExcess()
    {
        PropulsionUnit engine = Engine(EngineMode.Idle, 0.0);
        engine.UpdateReactor(0.0, false);
        engine.UpdateReactor(10000.0, false);

        Assert.AreEqual(0.0, engine.ExcessHeat);
    }

    [Test]
    public void Hull_SettlesToEquilibriumOfReceivedPower()
    {
        Hull hull = HotHull();
        double dt = 100000.0;
        hull.Accept(Flow.Heat, 0.5 * dt);
        hull.Settle(dt);

        Assert.AreEqual(90.0 + 200.0 * 0.5, hull.temperature, 1e-9);
    }

    [Test]
    public void Overheat_WearsEveryPanel()
    {
        ShipSystems systems = new();
        Hull hull = HotHull();
        hull.temperature = 450.0;
        systems.Add(hull);
        foreach (HullPanel panel in hull.panels) systems.Connect(hull, panel, Flow.Wear);

        systems.Update(0.0);
        systems.Update(1.0);

        foreach (HullPanel panel in hull.panels) Assert.Greater(panel.wear, 0.0);
    }

    [Test]
    public void ReactorHeat_ReachesHullInSameTick()
    {
        ShipSystems systems = new();
        PropulsionUnit engine = Engine(EngineMode.Prox, 1.0);
        Hull hull = HotHull();
        // Корпус добавлен раньше реактора: фазы тика не дают порядку что-то решать.
        systems.Add(hull);
        systems.Add(engine);
        systems.Connect(engine, hull, Flow.Heat);

        engine.UpdateReactor(0.0, false);
        systems.Update(0.0);
        engine.UpdateReactor(10.0, false);
        systems.Update(10.0);

        Assert.Greater(hull.temperature, 90.0);
        Assert.AreEqual(0.0, engine.ExcessHeat);
    }
}
