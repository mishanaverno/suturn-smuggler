using DoublePrecision;
using UnityEngine;
using Utilities;

namespace OuterSpace.Sim
{
    public class Maneuver : ObjectWithMono<ManeuverMono, Maneuver>
    {
        public SimTransform simTransform;
        public OrbitElements newOrbitParams;
        public Vector3d deltaLVLHVelocity = Vector3d.zero;
        public SpaceObject spaceObject;
        public double startEpoch;
        public readonly TrajectoryCache trajectory = new();

        // Точка манёвра относительно центрального тела, снятая при планировании. Манёвр — цель,
        // к которой игрок ведёт корабль: пока идёт прожиг, орбита корабля меняется, а точка стоит.
        // В simTransform её не сложить: там хранятся глобальные координаты, а центральное тело движется.
        Vector3d relativePosition;
        Vector3d relativeVelocity;

        public Maneuver(SpaceObject spaceObject, double startEpoch)
        {
            this.spaceObject = spaceObject;
            InstatiateGameObject(ResourcesLoader.LoadPrefab($"Sim/Maneuver"));
            GameObject.transform.parent = SimMono.instance.transform;
            simTransform = new(Vector3d.zero, Vector3d.zero, GameObject.transform);
            simTransform.RelativeTo = spaceObject.centralBody.simTransform;
            SetStartEpoch(startEpoch);
        }

        /// <summary>Перенос манёвра по времени: точка съезжает по текущей орбите корабля.</summary>
        public void SetStartEpoch(double epoch)
        {
            startEpoch = epoch;
            (relativePosition, relativeVelocity) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(spaceObject.orbitParams, epoch);
            CalcAndDraw();
        }

        /// <summary>Переносит точку манёвра в систему отсчёта нового центрального тела корабля.</summary>
        public void Reframe(SpaceObject previousCentral)
        {
            SimTransform current = spaceObject.centralBody.simTransform;
            relativePosition += previousCentral.simTransform.GLOBAL_R - current.GLOBAL_R;
            relativeVelocity += previousCentral.simTransform.GLOBAL_V - current.GLOBAL_V;
            simTransform.RelativeTo = current;
            CalcAndDraw();
        }

        /// <summary>Точка манёвра задана относительно центрального тела, а оно движется.</summary>
        public void FollowCentralBody()
        {
            simTransform.SetRELATIVE_R(relativePosition);
            simTransform.SetRELATIVE_V(relativeVelocity);
        }

        [ContextMenu("Update velocity")]
        public void CalcAndDraw()
        {
            Vector3d relDeltaV = CoordinateConverter.LocalDeltaVtoRelative(deltaLVLHVelocity, relativePosition, relativeVelocity);
            newOrbitParams = AstroDynamic.CalculateOrbitElements(relativePosition, relativeVelocity + relDeltaV, spaceObject.centralBody.MU, startEpoch);
            trajectory.Invalidate();
        }

        /// <summary>
        /// Прогноз считается от гипотетической орбиты манёвра, а не от текущего состояния корабля:
        /// игрок крутит deltaLVLHVelocity и должен видеть, куда приведёт получившаяся траектория.
        /// </summary>
        public void UpdateTrajectory(SpaceObject target) =>
            trajectory.Update(newOrbitParams, spaceObject.centralBody, startEpoch, target);
    }
}
