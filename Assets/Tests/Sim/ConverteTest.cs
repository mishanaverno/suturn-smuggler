using NUnit.Framework;
using OuterSpace.Sim;
using DoublePrecision;
public class ConverteTest
{
    private Vector3d positionRelative;
    private Vector3d velocityRelative;
    private Vector3d accelerationRelative;

    [SetUp]
    public void SetUp()
    {
        // Круговая орбита: 7000 км радиус, 7500 м/с скорость
        positionRelative = new Vector3d(7000e3, 0, 0);
        velocityRelative = new Vector3d(0, 0, 7500);

        // Центростремительное ускорение (аппроксимация)
        double a = -(7500 * 7500) / 7000e3;
        accelerationRelative = new Vector3d(a, 0, 0);
    }

    [Test]
    public void Test_LocalDeltaV_RoundTrip()
    {
        var deltaVLocal = new Vector3d(10, 1, -5);

        var rel = CoordinateConverter.LocalDeltaVtoRelative(deltaVLocal, positionRelative, velocityRelative);
        var localBack = CoordinateConverter.RelativeToLocal(rel, positionRelative, velocityRelative);

        AssertVectorsAreEqual(deltaVLocal, localBack, 1e-6);
    }

    [Test]
    public void Test_Position_RelativeToLocalAndBack()
    {
        var delta = new Vector3d(100, 20, -50);
        var rel = positionRelative + delta;

        var local = CoordinateConverter.RelativeToLocal(rel, positionRelative, velocityRelative);
        var relBack = CoordinateConverter.LocalDeltaVtoRelative(local, positionRelative, velocityRelative);

        AssertVectorsAreEqual(rel, relBack, 1e-3);
    }

    [Test]
    public void Test_Velocity_RelativeToLocalAndBack()
    {
        var vel = velocityRelative + new Vector3d(5, -2, 3);

        var local = CoordinateConverter.RelativeToLocal(vel, positionRelative, velocityRelative);
        var velBack = CoordinateConverter.LocalDeltaVtoRelative(local, positionRelative, velocityRelative);

        AssertVectorsAreEqual(vel, velBack, 1e-6);
    }

    [Test]
    public void Test_Acceleration_RelativeToLocalAndBack()
    {
        var acc = accelerationRelative + new Vector3d(0.1, 0.05, -0.2);

        var local = CoordinateConverter.RelativeToLocal(acc, positionRelative, velocityRelative);
        var accBack = CoordinateConverter.LocalDeltaVtoRelative(local, positionRelative, velocityRelative);

        AssertVectorsAreEqual(acc, accBack, 1e-6);
    }

    [Test]
    public void Test_LocalAxes_Orthonormality()
    {
        var R = positionRelative.normalized;
        var N = Vector3d.Cross(positionRelative, velocityRelative).normalized;
        var T = Vector3d.Cross(N, R);

        // Проверка длины
        Assert.AreEqual(1.0, R.magnitude, 1e-9);
        Assert.AreEqual(1.0, N.magnitude, 1e-9);
        Assert.AreEqual(1.0, T.magnitude, 1e-9);

        // Проверка ортогональности
        Assert.AreEqual(0.0, Vector3d.Dot(R, N), 1e-9);
        Assert.AreEqual(0.0, Vector3d.Dot(R, T), 1e-9);
        Assert.AreEqual(0.0, Vector3d.Dot(T, N), 1e-9);
    }

    [Test]
    public void Test_ZeroVectors_ShouldNotCrash()
    {
        var zero = new Vector3d(0, 0, 0);
        var result = CoordinateConverter.RelativeToLocal(zero, zero, zero);

        // Может быть любым (NaN или 0), но не должно кидать исключение
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void Test_CollinearVectors_ShouldNotCrash()
    {
        // position и velocity коллинеарны → N = 0
        var collinearPos = new Vector3d(1, 0, 0);
        var collinearVel = new Vector3d(2, 0, 0);
        var testVec = new Vector3d(0, 1, 0);

        Assert.DoesNotThrow(() =>
        {
            var local = CoordinateConverter.RelativeToLocal(testVec, collinearPos, collinearVel);
            Assert.That(local, Is.Not.Null);
        });
    }

    // 🔧 Утилита для сравнения векторов с допуском
    private void AssertVectorsAreEqual(Vector3d expected, Vector3d actual, double tolerance)
    {
        Assert.AreEqual(expected.x, actual.x, tolerance, "X component differs");
        Assert.AreEqual(expected.y, actual.y, tolerance, "Y component differs");
        Assert.AreEqual(expected.z, actual.z, tolerance, "Z component differs");
    }
}
