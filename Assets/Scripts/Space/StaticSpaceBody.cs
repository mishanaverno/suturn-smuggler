using UnityEngine;

namespace OuterSpace
{
    public class StaticSpaceObject
    {
        public double mass;
        public Vector3d position;
        public Vector3d relativePosition;

        public StaticSpaceObject(double mass, Vector3d position)
        {
            this.mass = mass;
            this.position = position;
        }
    }
}