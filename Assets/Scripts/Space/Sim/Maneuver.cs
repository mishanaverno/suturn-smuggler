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
        public Maneuver(SpaceObject spaceObject, double startEpoch)
        {
            this.spaceObject = spaceObject;
            InstatiateGameObject(ResourcesLoader.LoadPrefab($"Sim/Maneuver"));
            GameObject.transform.parent = SimMono.instance.transform;
            this.startEpoch = startEpoch;
            (Vector3d r, Vector3d v) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(spaceObject.orbitParams, startEpoch);
            simTransform = new(
                CoordinateConverter.RelativeToGlobal(r, spaceObject.centralBody.simTransform.GLOBAL_R),
                CoordinateConverter.RelativeToGlobal(v, spaceObject.centralBody.simTransform.GLOBAL_V),
                GameObject.transform
            );
            simTransform.RelativeTo = spaceObject.centralBody.simTransform;
        }
        [ContextMenu("Update velocity")]
        public void CalcAndDraw()
        {
            Vector3d relDeltaV = CoordinateConverter.LocalDeltaVtoRelative(deltaLVLHVelocity, simTransform.RELATIVE_R, simTransform.RELATIVE_V);
            Vector3d relV = simTransform.RELATIVE_V + relDeltaV;
            newOrbitParams = AstroDynamic.CalculateOrbitElements(simTransform.RELATIVE_R, relV, spaceObject.centralBody.MU, startEpoch);
        }
    }
}
