
using UnityEngine;

namespace OuterSpace {
    public class StarMono : CelestialBody, ICentralBody
    {
        public StaticSpaceObject spaceObject;
        public Vector3d Position => new(transform.position);
        public Vector3d RelativePostion => new(transform.position);
        public double Mass => spaceObject.mass;
        public double SOI => double.PositiveInfinity;

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
