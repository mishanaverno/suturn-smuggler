
using DoublePrecision;
using System.Collections.Generic;
using UnityEngine;

namespace OuterSpace.Sim.Objects
{
    public class CelestialBody : SpaceObject
    {
        public CelestialBody(Vector3d position, double mass, GameObject prefab) : base(position, Vector3d.zero, mass, prefab, new() { SpaceObjectParts.SOI, SpaceObjectParts.ORBIT })
        {

        }
    }
}
