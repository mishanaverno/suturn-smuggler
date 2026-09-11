using System.Collections.Generic;

namespace OuterSpace.Sim
{
    /// <summary>
    /// Посчитанный прогноз и признак того, что вход изменился. Прогноз стоит сотен вычислений
    /// положения, а меняется от нажатия клавиши, а не от хода времени, — поэтому считается
    /// по флагу, а не каждый кадр.
    /// </summary>
    public sealed class TrajectoryCache
    {
        public readonly PredictSettings settings = new();
        public IReadOnlyList<TrajectoryPatch> patches { get; private set; }
        public IReadOnlyList<CloseApproach> approaches { get; private set; }
        public SpaceObject target { get; private set; }

        // Прогноз, посчитанный от одного момента, стареет вместе с ходом времени: на быстрой
        // перемотке горизонт в тридцать суток проматывается за полминуты, и корабль въезжает
        // в сферу влияния по событию, которое давно уехало за конец посчитанной цепочки.
        // Поэтому окно едет: пересчёт, когда пройдена треть горизонта.
        const double RefreshFraction = 1.0 / 3.0;

        bool outdated = true;
        double computedEpoch;

        public void Invalidate() => outdated = true;

        /// <summary>
        /// Все посчитанные сближения пройдены. Именно все посчитанные, а не «сближений нет»:
        /// если в горизонте их не нашлось вовсе, пересчитывать нечего, и пустой список не
        /// должен превращаться в пересчёт каждые десять кадров.
        /// </summary>
        bool ApproachesSpent(double epoch) =>
            approaches != null && approaches.Count > 0 && approaches[approaches.Count - 1].Epoch <= epoch;

        public void Update(OrbitElements orbit, SpaceObject central, double startEpoch, SpaceObject target)
        {
            settings.horizon = settings.HorizonFor(orbit);
            bool stale = startEpoch - computedEpoch >= settings.horizon * RefreshFraction;
            // На стабильной орбите прогноз не протухает сам по себе, но сближения в нём
            // кончаются: корабль проходит их одно за другим. Когда пройдено последнее,
            // прибору нечего показывать, пока не досчитаны следующие.
            bool approachesSpent = target != null && target == this.target && ApproachesSpent(startEpoch);
            if (!outdated && !stale && !approachesSpent && target == this.target) return;
            outdated = false;
            computedEpoch = startEpoch;
            this.target = target;
            patches = TrajectoryPredictor.Predict(orbit, central, startEpoch, SimMono.bodies, settings);
            approaches = target == null ? null : TrajectoryPredictor.FindCloseApproaches(patches, target, settings);
        }
    }
}
