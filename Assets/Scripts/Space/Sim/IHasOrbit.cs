using DoublePrecision;
using UnityEngine;

namespace OuterSpace.Sim
{
    public interface IHasOrbit
    {
        public OrbitElements OrbitParams { get; }
        public Vector3d CenterPosition { get; }
    }
}
