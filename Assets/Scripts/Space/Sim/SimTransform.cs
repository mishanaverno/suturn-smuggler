
using UnityEngine;
using DoublePrecision;
using System;

namespace OuterSpace.Sim {
    public class SimTransform
    {
        private Vector3d position;
        public Transform transform;

        public Vector3d Position => position;
        public Vector3d LocalPosition => Position - ParentPosition;
        private Vector3d ParentPosition => Parent == null ? Vector3d.zero : Parent.SimTransform.Position;
        private ICentralBody Parent => transform.parent.GetComponentInParent<ICentralBody>();
        public SimTransform(Transform reprezentation, Vector3d vector)
        {
            this.transform = reprezentation;
            SetPosition(vector);
        }

        public void SetPosition(Vector3d position)
        {
            this.position = position;
            Vector3d vector = Position / Constanst.simDistanceMultiplier;
            transform.position = new Vector3((float)vector.x, (float)vector.y, (float)vector.z);
        }
        public void SetLocalPosition(Vector3d localPosition)
        {
            this.position = ParentPosition + localPosition;
            Vector3d vector = Position / Constanst.simDistanceMultiplier;
            transform.position = new Vector3((float)vector.x, (float)vector.y, (float)vector.z);
        }

        
    }
}