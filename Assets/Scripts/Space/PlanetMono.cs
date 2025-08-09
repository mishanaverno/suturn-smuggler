using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Space
{
    public class PlanetMono : CelestialBody
    {
        public DynamicSpaceObject spaceObject;
        public Vector3 velocity;

        public Vector3d Position => new(transform.position);

        public double Mass => spaceObject.mass;

        // Start is called before the first frame update
        void Awake()
        {
            spaceObject = new(M, GetComponentInParent<ICentralBody>());
        }
        private void Start()
        {
            spaceObject.UpdateVelocity(new(transform.position), new(velocity));
        }
        void FixedUpdate()
        {
            Vector3 pos = spaceObject.CalculatePositionAtTime(Time.time * 100) + spaceObject.centralbody.Position.CastToVector3();
            if(pos != Vector3.zero)
            {
                transform.position = pos;
            }
            
        }

    }
}
