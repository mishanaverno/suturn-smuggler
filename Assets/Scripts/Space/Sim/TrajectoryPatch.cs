using DoublePrecision;

namespace OuterSpace.Sim
{
    public enum PatchEndReason { Horizon, EnteredSOI, EscapedSOI, Impact }

    /// <summary>Одна коническая дуга прогноза: движение вокруг одного тела до ближайшего события.</summary>
    public sealed class TrajectoryPatch
    {
        public SpaceObject Central;
        public OrbitElements Orbit;
        public double StartEpoch;
        public double EndEpoch;
        public PatchEndReason EndReason;
        public SpaceObject NextCentral;
    }

    /// <summary>Точка наибольшего сближения с целью. Положения — абсолютные.</summary>
    public sealed class CloseApproach
    {
        public double Epoch;
        public double Distance;
        public double RelativeSpeed;
        public Vector3d ShipPosition;
        public Vector3d TargetPosition;
    }

    public sealed class PredictSettings
    {
        public double horizon = 30.0 * 86400.0;
        public int maxPatches = 5;
        // Шаг адаптивный, эти два — только границы: minStep задаёт точность у самой границы
        // сферы, maxStep не даёт пропустить событие на участке, где сближения нет вовсе.
        public double minStep = 1.0;
        public double maxStep = 3600.0;
        public double rootTolerance = 0.1;
        public int maxApproaches = 3;
        // Предфильтр по геометрии — оптимизация; выключается в тестах, чтобы проверить,
        // что он ничего не теряет.
        public bool prefilter = true;
        // Соорбитальная цель: расстояние почти не меняется, минимум размазан и смысла не имеет.
        public double coorbitalVariation = 0.01;
    }
}
