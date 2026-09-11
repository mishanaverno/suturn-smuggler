
using UnityEngine;
using DoublePrecision;

namespace OuterSpace.Sim {
    public class SimTransform
    {
        public Vector3d GLOBAL_R { get; private set; } // 
        public Vector3d RELATIVE_R => RelativeTo == null ? GLOBAL_R : CoordinateConverter.GlobalToRelative(GLOBAL_R, RelativeTo.GLOBAL_R); //
        public Vector3d LOCAL_R => RelativeTo == null ? GLOBAL_R : CoordinateConverter.RelativeToLocal(RELATIVE_R, RELATIVE_R, RELATIVE_V);

        public Vector3d GLOBAL_V { get; private set; } // 
        public Vector3d RELATIVE_V => RelativeTo == null ? GLOBAL_V : CoordinateConverter.GlobalToRelative(GLOBAL_V, RelativeTo.GLOBAL_V);
        public Vector3d LOCAL_V => RelativeTo == null ? GLOBAL_V : CoordinateConverter.RelativeToLocal(RELATIVE_V, RELATIVE_R, RELATIVE_V);

        /*public Vector3d GLOBAL_A; 
        public Vector3d RELATIVE_A; //
        public Vector3d LOCAL_A;*/

        public Transform SimReprezentation { get; private set; }

        public SimTransform(Vector3d gLOBAL_R, Vector3d gLOBAL_V, Transform simReprezentation)
        {
            GLOBAL_R = gLOBAL_R;
            GLOBAL_V = gLOBAL_V;
            SimReprezentation = simReprezentation;
            UpdateReprezentation();
        }

        public SimTransform RelativeTo { get; set; }
        public void SetGLOBAL_R(Vector3d GLOBAL)
        {
            GLOBAL_R = GLOBAL;
            UpdateReprezentation();
        }
        public void SetGLOBAL_V(Vector3d GLOBAL)
        {
            GLOBAL_V = GLOBAL;
        }
        public void SetRELATIVE_R(Vector3d RELATIVE)
        {
            GLOBAL_R = CoordinateConverter.RelativeToGlobal(RELATIVE, RelativeTo.GLOBAL_R);
            UpdateReprezentation();
        }
        public void SetRELATIVE_V(Vector3d RELATIVE)
        {
            GLOBAL_V = CoordinateConverter.RelativeToGlobal(RELATIVE, RelativeTo.GLOBAL_V);
        }
        /// <summary>Пересчитать положение в сцене после смены дальности или объекта наблюдения.</summary>
        public void Reproject() => UpdateReprezentation();
        private void UpdateReprezentation()
        {
            SimReprezentation.position = SimView.ToScene(GLOBAL_R);
        }

    }
}