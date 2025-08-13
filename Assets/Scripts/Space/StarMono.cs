
using UnityEngine;

namespace OuterSpace {
    public class StarMono : CelestialBody, ICentralBody
    {
        public StaticSpaceObject spaceObject;
        public Vector3d Position => new(transform.position);
        public Vector3d RelativePosition => new(transform.position);
        public Vector3d Velocity => Vector3d.zero;
        public double Mass => spaceObject.mass;
        public double SOI => double.PositiveInfinity;

        

        void Awake() {
            spaceObject = new(M,new(transform.position));
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
