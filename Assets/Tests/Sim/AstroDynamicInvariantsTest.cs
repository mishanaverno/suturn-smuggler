using System;
using System.Collections.Generic;
using NUnit.Framework;
using OuterSpace;
using OuterSpace.Sim;
using DoublePrecision;

/// <summary>
/// Инварианты, которым обязана удовлетворять любая кеплерова орбита.
/// Эталонных чисел здесь нет намеренно: эталон, снятый с проверяемой формулы,
/// ловит регрессию, но не ловит неверную формулу.
/// </summary>
public class AstroDynamicInvariantsTest
{
    const double MU = 398600441800000.0;
    const double ROUND_TRIP_TOL = 1e-9;
    const double DRIFT_TOL = 1e-10;

    public class OrbitCase
    {
        public readonly string name;
        public readonly Vector3d r;
        public readonly Vector3d v;
        public OrbitCase(string name, Vector3d r, Vector3d v) { this.name = name; this.r = r; this.v = v; }
        public override string ToString() => name;
    }

    static double Circular(double radius) => Math.Sqrt(MU / radius);

    static Vector3d RotateX(Vector3d a, double angle)
    {
        double c = Math.Cos(angle), s = Math.Sin(angle);
        return new Vector3d(a.x, a.y * c - a.z * s, a.y * s + a.z * c);
    }

    static IEnumerable<OrbitCase> AllOrbits()
    {
        double rc = 7000e3;
        double vc = Circular(rc);
        yield return new OrbitCase("circular equatorial",
            new Vector3d(rc, 0, 0), new Vector3d(0, vc, 0));
        // Наклонные случаи строятся поворотом плоской круговой орбиты вокруг оси X:
        // так объект оказывается в стороне от линии узлов, где acos не теряет половину разрядов.
        Vector3d rFlat = new Vector3d(rc * Math.Cos(1.0), rc * Math.Sin(1.0), 0);
        Vector3d vFlat = new Vector3d(-vc * Math.Sin(1.0), vc * Math.Cos(1.0), 0);
        yield return new OrbitCase("circular inclined", RotateX(rFlat, 0.6), RotateX(vFlat, 0.6));
        yield return new OrbitCase("circular polar", RotateX(rFlat, Math.PI / 2.0), RotateX(vFlat, Math.PI / 2.0));
        yield return new OrbitCase("circular retrograde equatorial", rFlat, -vFlat);
        yield return new OrbitCase("elliptic equatorial",
            new Vector3d(3000e3, 6000e3, 0), new Vector3d(-4000, 6000, 0));
        yield return new OrbitCase("elliptic inclined",
            new Vector3d(7000e3, 1000e3, 0), new Vector3d(500, 8000, 3000));
        yield return new OrbitCase("elliptic e > 0.9",
            new Vector3d(7000e3, 0, 0), new Vector3d(200, 10450, 900));
        yield return new OrbitCase("hyperbolic equatorial",
            new Vector3d(7000e3, 0, 0), new Vector3d(0, 13000, 0));
        yield return new OrbitCase("hyperbolic inclined",
            new Vector3d(7000e3, 500e3, 100e3), new Vector3d(1000, 11000, 4000));
    }

    static IEnumerable<OrbitCase> EllipticOrbits()
    {
        foreach (OrbitCase c in AllOrbits())
            if (AstroDynamic.CalculateOrbitElements(c.r, c.v, MU, 0.0).eccentricity < 1.0)
                yield return c;
    }

    static IEnumerable<OrbitCase> CircularOrbits()
    {
        foreach (OrbitCase c in AllOrbits())
            if (c.name.StartsWith("circular")) yield return c;
    }

    static double RelativeError(Vector3d expected, Vector3d actual) =>
        (expected - actual).magnitude / expected.magnitude;

    static string Describe(OrbitElements o) =>
        $"e = {o.eccentricity:G17}, i = {o.inclination:G17} deg, a = {o.semiMajorAxis:G17}";

    static void AssertState(Vector3d expectedR, Vector3d expectedV, Vector3d actualR, Vector3d actualV,
                            double tolerance, OrbitElements o, string what)
    {
        double dr = RelativeError(expectedR, actualR);
        double dv = RelativeError(expectedV, actualV);
        Assert.LessOrEqual(dr, tolerance, $"{what}: позиция, отн. погрешность {dr:E3} ({Describe(o)})");
        Assert.LessOrEqual(dv, tolerance, $"{what}: скорость, отн. погрешность {dv:E3} ({Describe(o)})");
    }

    [Test, TestCaseSource(nameof(AllOrbits))]
    public void RoundTrip_AtEpoch(OrbitCase c)
    {
        OrbitElements o = AstroDynamic.CalculateOrbitElements(c.r, c.v, MU, 0.0);
        (Vector3d r, Vector3d v) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(o, 0.0);
        AssertState(c.r, c.v, r, v, ROUND_TRIP_TOL, o, "round-trip при dt = 0");
    }

    [Test]
    public void RoundTrip_RandomOrbits()
    {
        System.Random rnd = new System.Random(7);
        int evaluated = 0;

        for (int i = 0; i < 2000; i++)
        {
            Vector3d r;
            do
            {
                r = new Vector3d(NextSigned(rnd) * 1e7, NextSigned(rnd) * 1e7, NextSigned(rnd) * 1e7);
            } while (r.magnitude < 1e6);

            double vc = 1.5 * Circular(r.magnitude);
            Vector3d v = new Vector3d(NextSigned(rnd) * vc, NextSigned(rnd) * vc, NextSigned(rnd) * vc);

            OrbitElements o = AstroDynamic.CalculateOrbitElements(r, v, MU, 0.0);

            // Околопараболические орбиты вырождены: r = p / (1 + e·cos nu) теряет точность
            // у асимптоты. Отдельный случай, к проверяемым инвариантам отношения не имеет.
            if (Math.Abs(o.eccentricity - 1.0) < 1e-3) continue;
            evaluated++;

            (Vector3d r2, Vector3d v2) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(o, 0.0);
            AssertState(r, v, r2, v2, ROUND_TRIP_TOL, o, $"round-trip случайной орбиты #{i}");
        }
        Assert.GreaterOrEqual(evaluated, 200, "проверено слишком мало орбит");
    }

    static double NextSigned(System.Random rnd) => rnd.NextDouble() * 2.0 - 1.0;

    [Test, TestCaseSource(nameof(AllOrbits))]
    public void SpecificEnergy_IsConserved(OrbitCase c)
    {
        OrbitElements o = AstroDynamic.CalculateOrbitElements(c.r, c.v, MU, 0.0);
        double expected = SpecificEnergy(c.r, c.v);

        foreach (double t in SampleTimes(o))
        {
            (Vector3d r, Vector3d v) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(o, t);
            double drift = Math.Abs(SpecificEnergy(r, v) - expected) / Math.Abs(expected);
            Assert.LessOrEqual(drift, DRIFT_TOL, $"дрейф энергии {drift:E3} при t = {t:G6} ({Describe(o)})");
        }
    }

    [Test, TestCaseSource(nameof(AllOrbits))]
    public void AngularMomentum_IsConserved(OrbitCase c)
    {
        OrbitElements o = AstroDynamic.CalculateOrbitElements(c.r, c.v, MU, 0.0);
        double expected = Vector3d.Cross(c.r, c.v).magnitude;

        foreach (double t in SampleTimes(o))
        {
            (Vector3d r, Vector3d v) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(o, t);
            double drift = Math.Abs(Vector3d.Cross(r, v).magnitude - expected) / expected;
            Assert.LessOrEqual(drift, DRIFT_TOL, $"дрейф момента {drift:E3} при t = {t:G6} ({Describe(o)})");
        }
    }

    static double SpecificEnergy(Vector3d r, Vector3d v) => Vector3d.Dot(v, v) / 2.0 - MU / r.magnitude;

    static double Period(OrbitElements o) => 2.0 * Math.PI * Math.Sqrt(Math.Pow(o.semiMajorAxis, 3) / MU);

    /// <summary>Несколько десятков витков для эллипса, сопоставимый пролёт для гиперболы.</summary>
    static IEnumerable<double> SampleTimes(OrbitElements o)
    {
        double span = o.eccentricity < 1.0 ? 30.0 * Period(o) : 100000.0;
        for (int i = 1; i <= 100; i++) yield return span * i / 100.0;
    }

    [Test, TestCaseSource(nameof(EllipticOrbits))]
    public void ReturnsToStart_AfterOnePeriod(OrbitCase c)
    {
        OrbitElements o = AstroDynamic.CalculateOrbitElements(c.r, c.v, MU, 0.0);
        (Vector3d r, Vector3d v) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(o, Period(o));
        AssertState(c.r, c.v, r, v, ROUND_TRIP_TOL, o, "состояние через период");
    }

    [Test, TestCaseSource(nameof(AllOrbits))]
    public void Propagation_IsReversibleInTime(OrbitCase c)
    {
        const double dt = 3000.0;
        OrbitElements o = AstroDynamic.CalculateOrbitElements(c.r, c.v, MU, 0.0);

        (Vector3d rb, Vector3d vb) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(o, -dt);
        OrbitElements back = AstroDynamic.CalculateOrbitElements(rb, vb, MU, -dt);
        (Vector3d r, Vector3d v) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(back, 0.0);

        AssertState(c.r, c.v, r, v, ROUND_TRIP_TOL, o, $"распространение на -{dt} и обратно");
    }

    [Test, TestCaseSource(nameof(CircularOrbits))]
    public void CircularOrbit_KeepsRadius(OrbitCase c)
    {
        OrbitElements o = AstroDynamic.CalculateOrbitElements(c.r, c.v, MU, 0.0);
        double expected = c.r.magnitude;
        double period = Period(o);

        for (int i = 0; i <= 100; i++)
        {
            Vector3d r = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(o, period * i / 100.0).r;
            double drift = Math.Abs(r.magnitude - expected) / expected;
            Assert.LessOrEqual(drift, DRIFT_TOL, $"радиус ушёл на {drift:E3} ({Describe(o)})");
        }
    }

    [TestCase(0.3)]
    [TestCase(0.9)]
    [TestCase(0.99)]
    [TestCase(1.6)]
    public void TrueAnomaly_SurvivesConversionThroughMeanAnomaly(double e)
    {
        // Состояние строится из nu в экваториальной плоскости, элементы дают M,
        // решатель Кеплера обязан вернуть исходную nu.
        double a = e < 1.0 ? 10000e3 : -10000e3;
        double p = Math.Abs(a) * Math.Abs(1.0 - e * e);
        double h = Math.Sqrt(MU * p);
        double limit = e < 1.0 ? Math.PI : Math.Acos(-1.0 / e);

        for (int i = -179; i <= 179; i++)
        {
            double nu = limit * i / 180.0;
            double r = p / (1.0 + e * Math.Cos(nu));
            double vr = (MU / h) * e * Math.Sin(nu);
            double vt = (MU / h) * (1.0 + e * Math.Cos(nu));

            Vector3d pos = new Vector3d(r * Math.Cos(nu), r * Math.Sin(nu), 0.0);
            Vector3d vel = new Vector3d(vr * Math.Cos(nu) - vt * Math.Sin(nu),
                                        vr * Math.Sin(nu) + vt * Math.Cos(nu), 0.0);

            OrbitElements o = AstroDynamic.CalculateOrbitElements(pos, vel, MU, 0.0);
            double actual = AstroDynamic.GetAnomalyesAtTime(o, 0.0).nu;

            double diff = Math.Abs(actual - (nu < 0 ? nu + 2.0 * Math.PI : nu));
            if (diff > Math.PI) diff = 2.0 * Math.PI - diff;
            Assert.LessOrEqual(diff, 1e-10, $"nu = {nu:G17} вернулась как {actual:G17} при e = {e}");
        }
    }
}
