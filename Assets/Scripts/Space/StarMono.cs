
using OuterSpace.Sim;
using DoublePrecision;

namespace OuterSpace {
    public class StarMono : CelestialBody, ICentralBody
    {
        public SpaceObject spaceObject;
        public Vector3d Position => new(transform.position);
        public Vector3d RelativePosition => new(transform.position);
        public Vector3d Velocity => Vector3d.zero;
        public double Mass => spaceObject.mass;
        public double SOI => double.PositiveInfinity;

        public SimTransform SimTransform => spaceObject.simTransform;

        void Awake() {
            spaceObject = new(transform, M * Constanst.simMassMultiplier, Vector3d.zero, Vector3d.zero, null);
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
