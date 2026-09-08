using System.Collections.Generic;
using DoublePrecision;
using NUnit.Framework;
using OuterSpace;
using OuterSpace.Sim;

public class SOITransitionTest
{
    SimTestWorld world;

    [SetUp]
    public void SetUp() => world = new SimTestWorld();

    [TearDown]
    public void TearDown() => world.Dispose();

    SpaceObject Resolve(SpaceObject obj) =>
        SOITransition.ResolveCentralBody(obj, obj.centralBody, world.bodies, SOITransition.Hysteresis);

    /// <summary>Объект внутри SOI луны, но со скоростью, заданной относительно центрального тела.</summary>
    SpaceObject ShipNearMoon(SpaceObject central, Vector3d offsetFromMoon, Vector3d velocityRelativeToCentral)
    {
        Vector3d relative = world.moon.simTransform.GLOBAL_R + offsetFromMoon - central.simTransform.GLOBAL_R;
        return world.CreateOrbiting(central, relative, velocityRelativeToCentral);
    }

    [Test]
    public void ChangeCentralBody_PreservesAbsolutePositionAndVelocity()
    {
        SpaceObject ship = ShipNearMoon(world.planet, new Vector3d(1.0e7, 0, 0), new Vector3d(0, 1100, 0));
        Vector3d position = ship.simTransform.GLOBAL_R;
        Vector3d velocity = ship.simTransform.GLOBAL_V;

        SOITransition.ChangeCentralBody(ship, world.moon, world.Epoch);

        AssertClose(position, ship.simTransform.GLOBAL_R, 1e-12);
        AssertClose(velocity, ship.simTransform.GLOBAL_V, 1e-12);
        Assert.AreSame(world.moon, ship.centralBody);
        Assert.AreEqual(world.Epoch, ship.orbitParams.startEpoch);
    }

    [Test]
    public void Descend_ObjectInsideMoonSOI_GetsMoon()
    {
        SpaceObject ship = ShipNearMoon(world.planet, new Vector3d(1.0e7, 0, 0), new Vector3d(0, 1100, 0));
        Assert.AreSame(world.moon, Resolve(ship));
    }

    [Test]
    public void Descend_OrbitIsClosedBelowEscapeSpeedAndOpenAbove()
    {
        const double radius = 1.0e7;
        double escape = System.Math.Sqrt(2.0 * SimTestWorld.MoonMU / radius);

        foreach (double factor in new[] { 0.7, 1.3 })
        {
            Vector3d moonRelativeVelocity = new(0, escape * factor, 0);
            SpaceObject ship = ShipNearMoon(
                world.planet,
                new Vector3d(radius, 0, 0),
                world.moon.simTransform.RELATIVE_V + moonRelativeVelocity);

            SOITransition.ChangeCentralBody(ship, Resolve(ship), world.Epoch);

            Assert.AreSame(world.moon, ship.centralBody);
            if (factor < 1.0)
                Assert.Less(ship.orbitParams.eccentricity, 1.0);
            else
                Assert.Greater(ship.orbitParams.eccentricity, 1.0);
        }
    }

    [Test]
    public void Ascend_ObjectBeyondMoonSOI_GetsPlanet()
    {
        SpaceObject ship = ShipNearMoon(world.moon, new Vector3d(world.moon.SOI * 1.5, 0, 0), new Vector3d(0, 900, 0));
        Assert.AreSame(world.planet, Resolve(ship));
    }

    [Test]
    public void ResolveCentralBody_CrossesTwoBoundariesInOneStep()
    {
        SpaceObject ship = ShipNearMoon(world.star, new Vector3d(1.0e7, 0, 0), new Vector3d(0, 30000, 0));
        Assert.AreSame(world.moon, Resolve(ship));
    }

    [Test]
    public void ExactlyOnSOIBoundary_ChangesParentAtMostOnce()
    {
        foreach (SpaceObject start in new[] { world.planet, world.moon })
        {
            SpaceObject ship = ShipNearMoon(start, new Vector3d(world.moon.SOI, 0, 0), new Vector3d(0, 900, 0));
            int changes = 0;
            for (int i = 0; i < 1000; i++)
            {
                SpaceObject central = Resolve(ship);
                if (central == ship.centralBody) continue;
                SOITransition.ChangeCentralBody(ship, central, world.Epoch);
                changes++;
            }
            Assert.LessOrEqual(changes, 1, $"старт от {start.GameObject.name}");
        }
    }

    [Test]
    public void EscapeFromMoon_KeepsTrajectoryContinuousAndSwitchesParent()
    {
        List<Vector3d> track = Simulate(out List<string> parents);

        Assert.AreNotEqual("Moon", parents[parents.Count - 1], "переход не состоялся");

        double previous = (track[1] - track[0]).magnitude;
        for (int i = 2; i < track.Count; i++)
        {
            double current = (track[i] - track[i - 1]).magnitude;
            Assert.That(System.Math.Abs(current - previous), Is.LessThan(0.1 * previous),
                $"скачок траектории на шаге {i}: {previous} -> {current}");
            previous = current;
        }
    }

    [Test]
    public void SameStepSequence_GivesSameParentChain()
    {
        Simulate(out List<string> first);

        world.Dispose();
        world = new SimTestWorld();

        Simulate(out List<string> second);

        CollectionAssert.AreEqual(first, second);
    }

    /// <summary>Уход по гиперболе от луны: 1000 шагов по 100 с с проверкой переходов на каждом.</summary>
    List<Vector3d> Simulate(out List<string> parents)
    {
        SpaceObject ship = world.CreateOrbiting(world.moon, new Vector3d(1.0e7, 0, 0), new Vector3d(1200, 900, 0));
        List<Vector3d> track = new() { ship.simTransform.GLOBAL_R };
        parents = new List<string> { ship.centralBody.GameObject.name };

        for (int i = 1; i <= 1000; i++)
        {
            double epoch = i * 100.0;
            world.Step(epoch, ship);

            SpaceObject central = Resolve(ship);
            if (central != ship.centralBody) SOITransition.ChangeCentralBody(ship, central, epoch);

            track.Add(ship.simTransform.GLOBAL_R);
            parents.Add(ship.centralBody.GameObject.name);
        }
        return track;
    }

    static void AssertClose(Vector3d expected, Vector3d actual, double relativeTolerance)
    {
        double tolerance = expected.magnitude * relativeTolerance;
        Assert.That((actual - expected).magnitude, Is.LessThanOrEqualTo(tolerance),
            $"expected {expected}, got {actual}");
    }
}
