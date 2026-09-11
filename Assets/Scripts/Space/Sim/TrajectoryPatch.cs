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
        // Горизонт считается от орбиты, а не задаётся снаружи: иначе корабль и манёвр смотрят
        // вперёд на разную глубину и прибор предупреждает об одном событии в разное время.
        public double horizonPeriods = 5.0;
        // Дугам после первой хватает пары витков: рисуется всё равно один виток, а лишние
        // нужны только чтобы не пропустить событие. Пять периодов на каждой из пяти дуг —
        // это тысячи шагов сканирования там, где ничего не происходит.
        public double chainHorizonPeriods = 2.0;
        // У незамкнутой орбиты периода нет. Суток хватает с запасом: гиперболический уход
        // от Титана до границы его сферы влияния занимает около семи часов.
        public double openOrbitHorizon = 86400.0;
        // Потолок на всю цепочку. Горизонт считается для каждой дуги от её собственной орбиты,
        // и без потолка цепочка из пяти дуг вокруг разных тел могла бы уехать на годы вперёд.
        public double maxHorizon = 120.0 * 86400.0;
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

        /// <summary>
        /// Горизонт для одной дуги: пять периодов её орбиты, а у незамкнутой — константа.
        /// Считается для каждой дуги отдельно, а не один раз для всей цепочки: гиперболический
        /// уход от луны живёт часы, а эллипс вокруг планеты за ним — недели, и общий горизонт,
        /// снятый с первой дуги, оставлял бы второй огрызок вместо орбиты.
        /// </summary>
        public double HorizonFor(OrbitElements orbit) => HorizonFor(orbit, horizonPeriods);

        public double HorizonFor(OrbitElements orbit, double periods) => orbit.eccentricity >= 1.0
            ? openOrbitHorizon
            : periods * AstroDynamic.Period(orbit);
    }
}
