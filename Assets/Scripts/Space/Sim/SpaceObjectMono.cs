using OuterSpace.Sim;
using DoublePrecision;
using UnityEngine;
using Utilities;
using Game;

namespace OuterSpace
{
    public class SpaceObjectMono : MonoWithObject<SpaceObject>, IHasOrbit, IHasSOI
    {
        public bool move = false;
        private bool prevMove = false;
        public SpaceObject spaceObject => Object;
        public OrbitElements OrbitParams => Object.orbitParams;
        public Vector3d CenterPosition => Object.centralBody.simTransform.GLOBAL_R;
        public double CentralSOI => Object.IsRoot ? double.PositiveInfinity : Object.centralBody.SOI;
        public Vector3d GlobalPosition => Object.simTransform.GLOBAL_R;
        public double SOI => Object.SOI;

        [Header("Vectors")]
        public Vector3d I_V;
        public Vector3d ECI_POS;
        public Vector3d ECI_VEL;
        public Vector3d LVLH_VEL;
        public double LVLH_VEL_MAG;

        public override void OnInstatiated()
        {
            base.OnInstatiated();
            gameObject.AddComponent<BodyGlyphMono>();
            if (Object.parts.Contains(SpaceObject.SpaceObjectParts.ORBIT))
            {
                Instantiate(ResourcesLoader.LoadPrefab($"Sim/Orbit"), transform);
            }
            if (Object.parts.Contains(SpaceObject.SpaceObjectParts.SOI))
            {
                Instantiate(ResourcesLoader.LoadPrefab($"Sim/SOI"), transform);
            }   
        }
        // Start is called before the first frame update
        private void Update()
        {
            Object.Update();
            if (Input.GetKeyDown(KeyCode.I) && spaceObject.centralBody != null)
            {
                Log();
            }
        }
        [ContextMenu("Body info")]
        public void Log()
        {
            Debug.Log($"\tObject name: {transform.name}\n" +
                $"Orbit:\n" +
                $"mu: {spaceObject.centralBody.MU}\n" +
                $"a: {spaceObject.orbitParams.semiMajorAxis}\n" +
                $"e: {spaceObject.orbitParams.eccentricity}\n" +
                $"i: {spaceObject.orbitParams.inclination}\n" +
                $"omega: {spaceObject.orbitParams.argumentOfPeriapsis}\n" +
                $"Omega: {spaceObject.orbitParams.longitudeOfAscendingNode}\n" +
                //$"nu: {spaceObject.orbitParams.trueAnomaly}\n" +
                $"SOI: {spaceObject.SOI}\n" +
                $"sim position: {spaceObject.simTransform.GLOBAL_R}\n" +
                $"relative sim position: {spaceObject.simTransform.RELATIVE_R}\n" +
                $"calculated sim position: {ECI_POS}\n" +
                $"position: {transform.position}\n" +
                $"relative velocity: {spaceObject.velocity}\n" +
                $"eci velocity: {ECI_VEL}\n" +
                $"lvlh velocity: {LVLH_VEL}\n" +
                $"expected v for circle orbit: {Mathd.Sqrt(spaceObject.centralBody.MU / spaceObject.simTransform.RELATIVE_R.magnitude)}\n" +
                $"\tCenter body: {spaceObject.centralBody.GameObject.name}\n" +
                $"SOI: {spaceObject.centralBody.SOI}\n" +
                $"position sim : {spaceObject.centralBody.simTransform.GLOBAL_R}\n" +
                $"relative sim position: {spaceObject.centralBody.simTransform.RELATIVE_R}\n" +
                $"position: {spaceObject.centralBody.simTransform.SimReprezentation.position}\n" +
                $"mu: {spaceObject.centralBody.MU}\n") ;


        }

    }
}
