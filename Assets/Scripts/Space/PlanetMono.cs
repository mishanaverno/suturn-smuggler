using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OuterSpace
{
    public class PlanetMono : CelestialBody, ICentralBody
    {
        public DynamicSpaceObject spaceObject;
        public Vector3 velocity;
        protected double _soi;
        public Vector3d RelativePostion => Position - spaceObject.centralbody.Position;

        public Vector3d Position => new(transform.position);
        public double Mass => spaceObject.mass;
        public double SOI => _soi;

        


        // Start is called before the first frame update
        void Awake()
        {
            spaceObject = new(M, transform.parent.GetComponentInParent<ICentralBody>());
        }
        private void Start()
        {
            spaceObject.UpdateVelocity(RelativePostion, new(velocity));
            _soi = spaceObject.orbitParams.semiMajorAxis * System.Math.Pow(spaceObject.mass / spaceObject.centralbody.Mass, 2.0 / 5.0);
        }
        void FixedUpdate()
        {
            Vector3 pos = spaceObject.CalculatePositionAtTime(Time.time * 100) + spaceObject.centralbody.Position.CastToVector3();
            if(pos != Vector3.zero)
            {
                transform.position = pos;
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
                $"relative position: {RelativePostion}\n" +
                $"velocity: {spaceObject.velocity}\n" +
                $"mass: {Mass}\n" +
                $"Center body:\n" + 
                $"SOI: {spaceObject.centralbody.SOI}\n" + 
                $"position: {spaceObject.centralbody.Position}\n" + 
                $"mass: {spaceObject.centralbody.Mass}\n");


        }

    }
}
