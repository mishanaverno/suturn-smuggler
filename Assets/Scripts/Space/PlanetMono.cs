using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OuterSpace
{
    public class PlanetMono : CelestialBody, ICentralBody
    {
        public DynamicSpaceObject spaceObject;
        /// <summary>
        ///  v = Mathd.sqrt(G*M/r)
        /// </summary>
        public Vector3 velocity;
        
        protected double _soi;
        public Vector3d RelativePosition => Position - spaceObject.centralBody.Position;

        public Vector3d Position => spaceObject.position;
        public double Mass => spaceObject.mass;
        public double SOI => _soi;

        public Vector3d Velocity => new(velocity);




        // Start is called before the first frame update
        void Awake()
        {
            spaceObject = new(M, new(transform.position), new(velocity), transform.parent.GetComponentInParent<ICentralBody>());
        }
        private void Start()
        {
            spaceObject.UpdateVelocity(RelativePosition, new(velocity));
            _soi = spaceObject.orbitParams.semiMajorAxis * Mathd.Pow((Mass) / (spaceObject.centralBody.Mass) , 2.0 / 5.0);
        }
        void FixedUpdate()
        {
            Vector3 pos = spaceObject.CalculatePositionAtTime(Time.time * 1000000) + spaceObject.centralBody.Position.CastToVector3();
            if(pos != Vector3.zero)
            {
                transform.position = pos;
                spaceObject.position = new(pos);
            }
            

        }
        public void Log()
        {
            Debug.Log($"Object name: {transform.name}" +
                $"\nOrbit:\n" +
                $"mu: {spaceObject.orbitParams.mu}\n" +
                $"a: {spaceObject.orbitParams.semiMajorAxis}\n" +
                $"e: {spaceObject.orbitParams.eccentricity}\n" +
                $"i: {spaceObject.orbitParams.inclination}\n" +
                $"omega: {spaceObject.orbitParams.argumentOfPericenter}\n" +
                $"Omega: {spaceObject.orbitParams.longitudeOfAscendingNode}\n" +
                $"SOI: {SOI}\n" +
                $"position: {transform.position}\n" +
                $"relative position: {RelativePosition}\n" +
                $"velocity: {spaceObject.velocity}\n" +
                $"mass: {Mass}\n" +
                $"Center body:\n" + 
                $"SOI: {spaceObject.centralBody.SOI}\n" + 
                $"position: {spaceObject.centralBody.Position}\n" + 
                $"mass: {spaceObject.centralBody.Mass}\n");


        }

    }
}
