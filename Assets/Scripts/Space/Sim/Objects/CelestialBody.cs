
using DoublePrecision;
using System.Collections.Generic;
using UnityEngine;

namespace OuterSpace.Sim.Objects
{
    public class CelestialBody : SpaceObject
    {
        public CelestialBody(double mu, GameObject prefab) : base(Vector3d.zero, Vector3d.zero, mu, prefab, new() { SpaceObjectParts.SOI, SpaceObjectParts.ORBIT })
        {

        }
    }
}
