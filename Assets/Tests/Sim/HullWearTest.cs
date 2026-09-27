using DoublePrecision;
using NUnit.Framework;
using OuterSpace.Sim;
using OuterSpace.Sim.Systems;

public class HullWearTest
{
    const double Rate = 0.001;

    static (ShipSystems, Hull) Build(Attitude attitude, Vector3d velocity)
    {
        ShipSystems systems = new();
        Hull hull = new();
        DustSource dust = new(attitude, () => velocity) { rate = Rate };
        systems.Add(dust);
        foreach (HullPanel panel in hull.panels)
        {
            systems.Add(panel);
            systems.Connect(dust, panel, Flow.Wear);
        }
        return (systems, hull);
    }

    [Test]
    public void NoseIntoFlow_WearsOnlyNose()
    {
        (ShipSystems systems, Hull hull) = Build(new Attitude(), new Vector3d(3000.0, 0.0, 0.0));
        systems.Update(0.0);
        systems.Update(10.0);

        Assert.AreEqual(Rate * 10.0, hull[HullSection.Nose].wear, 1e-12);
        foreach (HullPanel panel in hull.panels)
            if (panel != hull[HullSection.Nose]) Assert.AreEqual(0.0, panel.wear);
    }

    [Test]
    public void TurnedAround_WearsTail()
    {
        Attitude attitude = new() { rotation = Quaterniond.AngleAxis(180.0, Vector3d.forward) };
        (ShipSystems systems, Hull hull) = Build(attitude, new Vector3d(3000.0, 0.0, 0.0));
        systems.Update(0.0);
        systems.Update(10.0);

        Assert.AreEqual(Rate * 10.0, hull[HullSection.Tail].wear, 1e-9);
        Assert.AreEqual(0.0, hull[HullSection.Nose].wear);
    }

    [Test]
    public void FlowAtAngle_SplitsByCosine()
    {
        (ShipSystems systems, Hull hull) = Build(new Attitude(), new Vector3d(1.0, 0.0, 1.0));
        systems.Update(0.0);
        systems.Update(10.0);

        double expected = Rate * 10.0 * Mathd.Cos(45.0 * Mathd.Deg2Rad);
        Assert.AreEqual(expected, hull[HullSection.Nose].wear, 1e-12);
        Assert.AreEqual(expected, hull[HullSection.Top].wear, 1e-12);
        Assert.AreEqual(0.0, hull[HullSection.Left].wear);
    }

    [Test]
    public void Wear_StopsAtOne()
    {
        (ShipSystems systems, Hull hull) = Build(new Attitude(), new Vector3d(1.0, 0.0, 0.0));
        systems.Update(0.0);
        systems.Update(1e6);

        Assert.AreEqual(1.0, hull[HullSection.Nose].wear);
    }

    [Test]
    public void Update_WaitsForInterval_ThenGivesFullTime()
    {
        (ShipSystems systems, Hull hull) = Build(new Attitude(), new Vector3d(1.0, 0.0, 0.0));
        systems.Update(100.0);
        systems.Update(100.5);
        Assert.AreEqual(0.0, hull[HullSection.Nose].wear);

        systems.Update(130.0);
        Assert.AreEqual(Rate * 30.0, hull[HullSection.Nose].wear, 1e-12);
    }
}
