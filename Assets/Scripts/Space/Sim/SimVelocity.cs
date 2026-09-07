using DoublePrecision;
using UnityEngine;

namespace OuterSpace.Sim
{
    public class SimVelocity
    {
        public Vector3d global = Vector3d.zero;
        // статическая/инерциальная система координат
        // центр (0,0,0) совпадает с центром массы звезды
        // x - направлена Vector3d.right
        // y - направлена Vector3d.up;
        // z - направлена Vector3d.forwarf

        public Vector3d relative = Vector3d.zero;
        // статическая/инерциальная система координат
        // центр совпадает с центром небесного тела на орбите которого происходит движение
        // x - направлена Vector3d.right
        // y - направлена Vector3d.up; // возможно переделаем так чтобы планеты имели наклонную ось
        // z - направлена Vector3d.forwarf

        public Vector3d local = Vector3d.zero;
        // динамическая/не инерциальная система координат
        // центр совпадает 

    }
}
