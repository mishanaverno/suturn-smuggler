using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OuterSpace
{
    public interface ICentralBody
    {
        public Vector3d Position { get; }

        public Vector3d RelativePosition { get; }
        public Vector3d Velocity { get; }
        public double Mass { get; }
        public double SOI { get; }
    }
}
