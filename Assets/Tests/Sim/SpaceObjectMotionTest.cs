using System;
using DoublePrecision;
using NUnit.Framework;
using OuterSpace;
using OuterSpace.Sim;

public class SpaceObjectMotionTest
{
    SimTestWorld world;

    [SetUp]
    public void SetUp() => world = new SimTestWorld();

    [TearDown]
    public void TearDown() => world.Dispose();

    [Test]
    public void RelativeVelocity_MatchesAstroDynamic_AfterManySteps()
    {
        for (int i = 1; i <= 200; i++)
        {
            world.Step(i * 500.0);
        }

        foreach (SpaceObject body in new[] { world.planet, world.moon })
        {
            (_, Vector3d expected) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(body.orbitParams, world.Epoch);
            AssertClose(expected, body.simTransform.RELATIVE_V, 1e-12);
        }
    }

    [Test]
    public void NumericDerivativeOfPosition_MatchesRecordedVelocity()
    {
        const double t = 1.0e5;
        const double dt = 1.0;

        world.Step(t - dt);
        Vector3d before = world.moon.simTransform.GLOBAL_R;
        world.Step(t + dt);
        Vector3d after = world.moon.simTransform.GLOBAL_R;
        world.Step(t);
        Vector3d velocity = world.moon.simTransform.GLOBAL_V;

        AssertClose((after - before) / (2.0 * dt), velocity, 1e-6);
    }

    [Test]
    public void GlobalVelocity_EqualsSumOfRelativeVelocitiesAlongParentChain()
    {
        SpaceObject ship = world.CreateOrbiting(
            world.moon,
            new Vector3d(0, 2.0e7, 0),
            new Vector3d(SimTestWorld.CircularSpeed(world.moon.MU, 2.0e7), 0, 0));

        world.Step(3.0e5, ship);

        Vector3d sum = world.star.simTransform.GLOBAL_V;
        for (SpaceObject obj = ship; obj != null && !obj.IsRoot; obj = obj.centralBody)
        {
            sum += obj.simTransform.RELATIVE_V;
        }

        AssertClose(sum, ship.simTransform.GLOBAL_V, 1e-12);
    }

    static void AssertClose(Vector3d expected, Vector3d actual, double relativeTolerance)
    {
        double tolerance = expected.magnitude * relativeTolerance;
        Assert.That((actual - expected).magnitude, Is.LessThanOrEqualTo(tolerance),
            $"expected {expected}, got {actual}");
    }
}
