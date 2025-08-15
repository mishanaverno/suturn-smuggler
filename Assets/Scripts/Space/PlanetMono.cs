using OuterSpace.Sim;
using DoublePrecision;
using UnityEngine;


namespace OuterSpace
{
    public class PlanetMono : CelestialBody, ICentralBody
    {
        public bool move = false;
        public SpaceObject spaceObject;
        /// <summary>
        ///  v = Mathd.sqrt(G*M/r)
        /// </summary>
        protected double _soi;
        public double Mass => spaceObject.mass;
        public double SOI => _soi;
        public Vector3d Velocity => spaceObject.velocity;

        public SimTransform SimTransform => spaceObject.simTransform;

        // Start is called before the first frame update
        void Awake()
        {
            spaceObject = new(transform, M * Constanst.simMassMultiplier, new Vector3d(PX, PY, PZ) * Constanst.simDistanceMultiplier, new Vector3d(VX, VY, VZ), transform.parent.GetComponentInParent<ICentralBody>());
        }
        private void Start()
        {
            SimTransform.SetLocalPosition(new Vector3d(PX, PY, PZ) * Constanst.simDistanceMultiplier);
            spaceObject.UpdateVelocity(SimTransform.LocalPosition, Velocity);
            _soi = spaceObject.orbitParams.semiMajorAxis * Mathd.Pow((Mass) / (spaceObject.centralBody.Mass) , 2.0 / 5.0);
        }
        void FixedUpdate()
        {
            
            if (move)
            {
                Vector3d newPos = spaceObject.CalculatePositionAtTime(Time.time * 1000000) + spaceObject.centralBody.SimTransform.Position;
                if (newPos != Vector3d.zero)
                {
                    SimTransform.SetPosition(newPos);
                }
            }
            else
            {
                _soi = spaceObject.orbitParams.semiMajorAxis * System.Math.Pow(Mass / spaceObject.centralBody.Mass, 2.0 / 5.0);
                spaceObject.UpdateVelocity(SimTransform.LocalPosition, new(VX, VY, VZ));
            }
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Log();
            }

        }
        public void Log()
        {
            Debug.Log($"\tObject name: {transform.name}\n" +
                $"Orbit:\n" +
                $"mu: {spaceObject.orbitParams.mu}\n" +
                $"a: {spaceObject.orbitParams.semiMajorAxis}\n" +
                $"e: {spaceObject.orbitParams.eccentricity}\n" +
                $"i: {spaceObject.orbitParams.inclination}\n" +
                $"omega: {spaceObject.orbitParams.argumentOfPericenter}\n" +
                $"Omega: {spaceObject.orbitParams.longitudeOfAscendingNode}\n" +
                $"SOI: {SOI}\n" +
                $"sim position: {SimTransform.Position}\n" +
                $"relative sim position: {SimTransform.LocalPosition}\n" +
                $"calculated sim position: {SimTransform.LocalPosition + spaceObject.centralBody.SimTransform.Position}\n" +
                $"position: {transform.position}\n" +
                $"local position: {transform.localPosition}\n" +
                $"calculated position: {transform.localPosition + spaceObject.centralBody.SimTransform.transform.position}\n" +
                $"velocity: {spaceObject.velocity}\n" +
                $"mass: {Mass}\n" +
                $"expected v: {Mathd.Sqrt((Constanst.realG * spaceObject.centralBody.Mass) / spaceObject.simTransform.LocalPosition.magnitude)}\n" +
                $"\tCenter body:\n" +
                $"SOI: {spaceObject.centralBody.SOI}\n" +
                $"position sim : {spaceObject.centralBody.SimTransform.Position}\n" +
                $"relative sim position: {spaceObject.centralBody.SimTransform.LocalPosition}\n" +
                $"position: {spaceObject.centralBody.SimTransform.transform.position}\n" +
                $"local position: {spaceObject.centralBody.SimTransform.transform.localPosition}\n" +
                $"mass: {spaceObject.centralBody.Mass}\n") ;


        }

    }
}
