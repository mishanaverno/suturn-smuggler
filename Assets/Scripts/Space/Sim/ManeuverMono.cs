using DoublePrecision;
using UnityEngine;
using Utilities;

namespace OuterSpace.Sim
{
    public class ManeuverMono : MonoWithObject<Maneuver>, IHasOrbit
    {
        public OrbitElements OrbitParams => Object.newOrbitParams;

        public Vector3d CenterPosition => Object.spaceObject.centralBody.simTransform.GLOBAL_R;

        public override void OnInstatiated()
        {
            base.OnInstatiated();
            Instantiate(ResourcesLoader.LoadPrefab($"Sim/Orbit"), transform);
        }
        void FixedUpdate()
        {
            (Vector3d R, _) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(Object.spaceObject.orbitParams, Object.startEpoch);
            Object.simTransform.SetRELATIVE_R(R);
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
