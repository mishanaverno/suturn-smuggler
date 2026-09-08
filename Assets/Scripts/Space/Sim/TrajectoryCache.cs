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

        bool outdated = true;

        public void Invalidate() => outdated = true;

        public void Update(OrbitElements orbit, SpaceObject central, double startEpoch, SpaceObject target)
        {
            if (!outdated && target == this.target) return;
            outdated = false;
            this.target = target;
            patches = TrajectoryPredictor.Predict(orbit, central, startEpoch, SimMono.bodies, settings);
            approaches = target == null ? null : TrajectoryPredictor.FindCloseApproaches(patches, target, settings);
        }
    }
}
