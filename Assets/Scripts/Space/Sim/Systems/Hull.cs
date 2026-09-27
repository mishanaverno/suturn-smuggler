using DoublePrecision;

namespace OuterSpace.Sim.Systems
{
    public enum HullSection
    {
        Nose,
        Tail,
        Left,
        Right,
        Top,
        Bottom,
    }

    public class HullPanel : SystemNode
    {
        /// <summary>Внешняя нормаль в связанных осях: X — вперёд, Y — влево, Z — вверх.</summary>
        public readonly Vector3d normal;

        public HullPanel(Vector3d normal) => this.normal = normal;
    }

    /// <summary>Корпус — шесть панелей по граням связанных осей. Каждая изнашивается сама.</summary>
    public class Hull
    {
        public readonly HullPanel[] panels =
        {
            new(Vector3d.right),
            new(-Vector3d.right),
            new(Vector3d.up),
            new(-Vector3d.up),
            new(Vector3d.forward),
            new(-Vector3d.forward),
        };

        public HullPanel this[HullSection section] => panels[(int)section];
    }
}
