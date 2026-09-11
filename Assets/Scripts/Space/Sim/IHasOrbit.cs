using DoublePrecision;
using UnityEngine;

namespace OuterSpace.Sim
{
    public interface IHasOrbit
    {
        public OrbitElements OrbitParams { get; }
        public Vector3d CenterPosition { get; }
        /// <summary>Сфера влияния центрального тела: за ней коника уже не траектория.</summary>
        public double CentralSOI { get; }
    }
}
