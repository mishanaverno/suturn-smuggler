using Space;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

namespace Space {
    public class StarMono : CelestialBody, ICentralBody
    {
        public StaticSpaceObject spaceObject;
        public Vector3d Position => new(transform.position);
        public double Mass => spaceObject.mass;

        void Awake() {
            spaceObject = new(M);
        }
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}
