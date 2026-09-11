using DoublePrecision;
using UnityEngine;

namespace OuterSpace.Sim.Objects
{
    public class RootBody : SpaceObject
    {
        public RootBody(double mu, GameObject prefab) : base(Vector3d.zero, Vector3d.zero, mu, prefab, new()) { }
    }
}
