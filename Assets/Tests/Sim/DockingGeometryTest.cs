using DoublePrecision;
using Game;
using NUnit.Framework;
using OuterSpace.Sim;

/// <summary>
/// Узел станции смотрит по -Y, верх — Z; узел корабля левый, по +Y корпуса. При единичной
/// ориентации корабля узлы соосны и смотрят друг на друга.
/// </summary>
public class DockingGeometryTest
{
    const double Tolerance = 1e-9;
    static readonly PortData StationPort = new()
    {
        position = new Vector3d(0, -20, 0),
        axis = new Vector3d(0, -1, 0),
        up = new Vector3d(0, 0, 1),
    };
    static readonly PortData ShipPort = new()
    {
        position = new Vector3d(0, 2.5, 0),
        axis = new Vector3d(0, 1, 0),
        up = new Vector3d(0, 0, 1),
    };
    // Узел корабля в 10 м перед узлом станции.
    static readonly Vector3d AlignedShip = new(0, -32.5, 0);

    static DockingState Measure(Vector3d shipR, Quaterniond rotation, Vector3d shipV = default) =>
        DockingGeometry.Measure(shipR, shipV, rotation, Vector3d.zero, ShipPort, Vector3d.zero, Vector3d.zero, StationPort);

    [Test]
    public void Coaxial_GivesRangeAndNoMisalignment()
    {
        DockingState state = Measure(AlignedShip, Quaterniond.identity);
        Assert.AreEqual(10.0, state.Range, Tolerance);
        Assert.AreEqual(0.0, state.Lateral, Tolerance);
        Assert.AreEqual(0.0, state.Roll, Tolerance);
        Assert.AreEqual(0.0, state.Pitch, Tolerance);
        Assert.AreEqual(0.0, state.Yaw, Tolerance);
    }

    [Test]
    public void ShiftedUp_ShowsInUpOnly()
    {
        DockingState state = Measure(AlignedShip + new Vector3d(0, 0, 0.4), Quaterniond.identity);
        Assert.AreEqual(0.4, state.Up, Tolerance);
        Assert.AreEqual(0.0, state.Side, Tolerance);
        Assert.AreEqual(10.0, state.Range, Tolerance);
    }

    [Test]
    public void MovingTowardPort_IsPositiveClosing()
    {
        DockingState state = Measure(AlignedShip, Quaterniond.identity, new Vector3d(0, 0.2, 0));
        Assert.AreEqual(0.2, state.ClosingSpeed, Tolerance);
    }

    // Поворот вокруг оси узла станции — крен, вокруг верха — рысканье, вокруг бока — тангаж.
    // Каждый поворот обязан проявиться ровно в своём угле.
    [TestCase(0, 1, 0, 2.0, 0.0, 0.0)]
    [TestCase(0, 0, 1, 0.0, 0.0, 2.0)]
    [TestCase(1, 0, 0, 0.0, 2.0, 0.0)]
    public void RotationAboutOneAxis_ShowsInOneAngle(double x, double y, double z, double roll, double pitch, double yaw)
    {
        Quaterniond rotation = Quaterniond.AngleAxis(2.0, new Vector3d(x, y, z));
        DockingState state = Measure(AlignedShip, rotation);
        Assert.AreEqual(roll, System.Math.Abs(state.Roll), 1e-6, "крен");
        Assert.AreEqual(pitch, System.Math.Abs(state.Pitch), 1e-6, "тангаж");
        Assert.AreEqual(yaw, System.Math.Abs(state.Yaw), 1e-6, "рысканье");
    }
}
