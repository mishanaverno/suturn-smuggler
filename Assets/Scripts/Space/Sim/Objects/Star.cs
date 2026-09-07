using DoublePrecision;
using UnityEngine;

namespace OuterSpace.Sim.Objects
{
    public class Star : SpaceObject
    {
        public Star(Vector3d position, double mass, GameObject prefab) : base(position, Vector3d.zero, mass, prefab, new()) { }
    }
}
