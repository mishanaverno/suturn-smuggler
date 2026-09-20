using System.Collections.Generic;
using System.Text;
using DoublePrecision;
using OuterSpace.Sim.Objects;
using UnityEngine;
using Utilities;

namespace OuterSpace.Sim
{
    public class ManeuverMono : MonoWithObject<Maneuver>, IHasTrajectory
    {
        // Прогноз не должен считаться каждый кадр: вход у него меняется от нажатия клавиши,
        // а не от хода времени.
        const int RecalculateEveryFrames = 10;
        // Запасной цвет нужен до появления NavDisplay; в игре цвета задаёт сам прибор.
        static readonly Color DefaultManeuverColor = new(0.75f, 0.45f, 1f);
        TrajectoryRenderer trajectoryRenderer;
        public IReadOnlyList<TrajectoryPatch> Patches => Object.trajectory.patches;
        bool IsLast => Object.spaceObject is Ship ship && ship.GetManeuver() == Object;
        public IReadOnlyList<CloseApproach> Approaches => IsLast ? Object.trajectory.approaches : null;
        public SpaceObject Target => IsLast ? Object.trajectory.target : null;

        public override void OnInstatiated()
        {
            base.OnInstatiated();
            // Одной орбиты вокруг одного центра здесь мало: получившаяся траектория может
            // уйти в чужую сферу влияния, и рисовать её надо цепочкой дуг.
            trajectoryRenderer = gameObject.AddComponent<TrajectoryRenderer>();
            trajectoryRenderer.color = NavDisplayMono.instance == null
                ? DefaultManeuverColor
                : NavDisplayMono.instance.ManeuverColor(Object.SequenceIndex);
            trajectoryRenderer.approachColor = new(0.6f, 0.5f, 0.2f);
            trajectoryRenderer.markStart = true;
            // Точку манёвра он же рисует звёздочкой фиксированного экранного размера, а шарик
            // из префаба на дальних масштабах превращался в пятно без смысла.
            foreach (MeshRenderer mesh in GetComponentsInChildren<MeshRenderer>()) mesh.enabled = false;
        }
        void LateUpdate()
        {
            Object.FollowCentralBody();
            if (NavDisplayMono.instance != null)
            {
                trajectoryRenderer.color = NavDisplayMono.instance.ManeuverColor(Object.SequenceIndex);
            }
            if (Time.frameCount % RecalculateEveryFrames != 0) return;
            SpaceObject target = Object.spaceObject is Ship ship
                ? ship.TargetForTrajectory(Object)
                : SimMono.target;
            Object.UpdateTrajectory(target);
        }
        /// <summary>
        /// Разбирать артефакты отрисовки по скриншоту дорого: последнее число здесь —
        /// максимальный угол между соседними точками дуги, видимый из фокуса, — сразу
        /// отвечает на вопрос «кривая или ломаная».
        /// </summary>
        [ContextMenu("Trajectory info")]
        public void LogTrajectory()
        {
            IReadOnlyList<TrajectoryPatch> patches = Object.trajectory.patches;
            if (patches == null)
            {
                Debug.Log("TRAJECTORY[] прогноза ещё нет");
                return;
            }
            List<Vector3d> points = new();
            StringBuilder report = new("TRAJECTORY[]\n");
            for (int i = 0; i < patches.Count; i++)
            {
                TrajectoryPatch patch = patches[i];
                AstroDynamic.SampleArc(patch.Orbit, patch.StartEpoch, patch.EndEpoch,
                    TrajectoryRenderer.MaxPointsPerPatch, points);
                double period = patch.Orbit.eccentricity < 1.0
                    ? 2.0 * Mathd.PI * Mathd.Sqrt(Mathd.Pow(patch.Orbit.semiMajorAxis, 3) / patch.Orbit.mu)
                    : double.PositiveInfinity;
                report.AppendLine(
                    $"{i}: {patch.Central.GameObject.name} e={patch.Orbit.eccentricity:F4} a={patch.Orbit.semiMajorAxis:E3} " +
                    $"period={period:F0} span={patch.EndEpoch - patch.StartEpoch:F0} {patch.EndReason} " +
                    $"points={points.Count} maxAngle={MaxAngle(points):F1}");
            }
            Debug.Log(report.ToString());
        }

        static double MaxAngle(List<Vector3d> points)
        {
            double worst = 0.0;
            for (int i = 1; i < points.Count; i++)
            {
                worst = Mathd.Max(worst, Vector3d.Angle(points[i - 1], points[i]));
            }
            return worst;
        }

        [ContextMenu("Maneuver info")]
        public void Log()
        {
            Debug.Log(Object.deltaLVLHVelocity);
            Debug.Log(Object.newOrbitParams);
            Debug.Log(
                $"MANEUVER[]\n" +
                $"deltaV: {Object.deltaLVLHVelocity}\n" +
                $"RelativeV: {Object.simTransform.RELATIVE_V}\n" +
                $"a: {Object.newOrbitParams.semiMajorAxis}\n" +
                $"e: {Object.newOrbitParams.eccentricity}\n" +
                $"i: {Object.newOrbitParams.inclination}\n" +
                $"omega: {Object.newOrbitParams.argumentOfPeriapsis}\n" +
                $"Omega: {Object.newOrbitParams.longitudeOfAscendingNode}\n" +
                $"nu: \n"
            );
        }
    }
}
