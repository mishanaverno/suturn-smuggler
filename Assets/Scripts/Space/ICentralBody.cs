using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Space
{
    public interface ICentralBody
    {
        public Vector3d Position { get; }
        public double Mass { get; }
    }
}
