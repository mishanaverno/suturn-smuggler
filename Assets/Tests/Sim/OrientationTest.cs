using System;
using DoublePrecision;
using NUnit.Framework;
using OuterSpace;
using OuterSpace.Sim;

public class OrientationTest
{
    const double TitanMU = 8978137100000.0;
    const double Radius = 2975000.0;

    static readonly Vector3d R = new(Radius, 0.0, 0.0);
    static readonly Vector3d V = new(0.0, 1504.5, 868.6);
    static readonly Vector3d Closing = new(-120.0, 45.0, 30.0);
    static readonly Vector3d Planned = new(70.0, -20.0, 10.0);

    static readonly ShipOrientation[] Aimed =
    {
        ShipOrientation.Prograde, ShipOrientation.Retrograde,
        ShipOrientation.Normal, ShipOrientation.Antinormal,
        ShipOrientation.RadialOut, ShipOrientation.RadialIn,
        ShipOrientation.Target, ShipOrientation.AntiTarget,
        ShipOrientation.Maneuver,
    };

    static Vector3d Direction(ShipOrientation mode, Vector3d r, Vector3d v) =>
        Orientation.Direction(mode, r, v, Closing, Planned);

    static OrbitElements Orbit() => AstroDynamic.CalculateOrbitElements(R, V, TitanMU, 0.0);

    [Test]
    public void EveryMode_GivesUnitVector()
    {
        foreach (ShipOrientation mode in Aimed)
        {
            Assert.AreEqual(1.0, Direction(mode, R, V).magnitude, 1e-12, mode.ToString());
        }
        Assert.AreEqual(Vector3d.zero, Direction(ShipOrientation.Free, R, V));
    }

    [Test]
    public void Triad_IsOrthogonal()
    {
        Vector3d prograde = Direction(ShipOrientation.Prograde, R, V);
        Vector3d normal = Direction(ShipOrientation.Normal, R, V);
        Vector3d radial = Direction(ShipOrientation.RadialOut, R, V);

        Assert.AreEqual(0.0, Vector3d.Dot(prograde, normal), 1e-12);
        Assert.AreEqual(0.0, Vector3d.Dot(normal, radial), 1e-12);
        Assert.AreEqual(0.0, Vector3d.Dot(radial, prograde), 1e-12);
    }

    [Test]
    public void Prograde_And_Normal_MatchVelocityAndAngularMomentum()
    {
        Vector3d prograde = Direction(ShipOrientation.Prograde, R, V);
        Vector3d normal = Direction(ShipOrientation.Normal, R, V);

        Assert.AreEqual(0.0, (prograde - V.normalized).magnitude, 1e-12);
        Assert.AreEqual(0.0, (normal - Vector3d.Cross(R, V).normalized).magnitude, 1e-12);
    }

    [Test]
    public void Maneuver_IsFrozen_While_OthersFollowMotion()
    {
        OrbitElements orbit = Orbit();
        double quarter = 0.25 * AstroDynamic.Period(orbit);
        (Vector3d r0, Vector3d v0) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(orbit, 0.0);
        (Vector3d r1, Vector3d v1) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(orbit, quarter);

        Assert.AreEqual(0.0,
            (Direction(ShipOrientation.Maneuver, r0, v0) - Direction(ShipOrientation.Maneuver, r1, v1)).magnitude,
            1e-12);

        // Нормали в списке нет: на невозмущённой конике плоскость орбиты не поворачивается,
        // и постоянство нормали — свойство самой орбиты, а не заморозки режима.
        foreach (ShipOrientation mode in new[] { ShipOrientation.Prograde, ShipOrientation.RadialOut })
        {
            double moved = (Direction(mode, r0, v0) - Direction(mode, r1, v1)).magnitude;
            Assert.Greater(moved, 1e-3, mode.ToString());
        }
    }

    [Test]
    public void Thrust_AlongMode_ChangesTheRightElement()
    {
        const double Impulse = 1.0;
        OrbitElements orbit = Orbit();

        double prograde = Burn(ShipOrientation.Prograde, Impulse).semiMajorAxis;
        double retrograde = Burn(ShipOrientation.Retrograde, Impulse).semiMajorAxis;
        OrbitElements normal = Burn(ShipOrientation.Normal, Impulse);

        Assert.Greater(prograde, orbit.semiMajorAxis);
        Assert.Less(retrograde, orbit.semiMajorAxis);
        Assert.AreNotEqual(orbit.inclination, normal.inclination);

        // Нормаль поворачивает плоскость, а не растягивает орбиту: изменение большой полуоси
        // второго порядка по импульсу, у програда — первого.
        double alongTrack = Math.Abs(prograde - orbit.semiMajorAxis);
        Assert.Less(Math.Abs(normal.semiMajorAxis - orbit.semiMajorAxis), alongTrack * 1e-2);
    }

    static OrbitElements Burn(ShipOrientation mode, double impulse) =>
        AstroDynamic.CalculateOrbitElements(R, V + Direction(mode, R, V) * impulse, TitanMU, 0.0);
}
