using DoublePrecision;


namespace OuterSpace.Sim
{
    public static class CoordinateConverter
    {
        // GLOBAL
        // статическая/инерциальная система координат
        // центр (0,0,0) совпадает с центром массы звезды
        // x - направлена Vector3d.right
        // y - направлена Vector3d.up;
        // z - направлена Vector3d.forwarf

        // RELATIVE
        // статическая/инерциальная система координат
        // центр совпадает с центром небесного тела на орбите которого происходит движение
        // x - направлена Vector3d.right
        // y - направлена Vector3d.up; // возможно переделаем так чтобы планеты имели наклонную ось
        // z - направлена Vector3d.forwarf

        // LOCAL
        // динамическая/не инерциальная система координат
        // центр совпадает с центром тела которое движется по орбите
        // x - T тангенциальное движение по орбите, направлена в проград
        // y - N перпендикулярна плоскости орбиты, дополняет систему до правосторонней
        // z - R направлена от центра массы центрального тела к центру массы тела на орбите
        //

        // Основные системы координат
        public enum CoordinateSystem { GLOBAL, RELATIVE, LOCAL }

        // 
        public static Vector3d RelativeToGlobal(Vector3d relative, Vector3d centerGlobal)
        {
            return relative + centerGlobal;
        }

        // 
        public static Vector3d GlobalToRelative(Vector3d global, Vector3d centerGlobal)
        {
            return global - centerGlobal;
        }
        public static Vector3d RelativeToLocal(Vector3d relativeVector, Vector3d positionRelative, Vector3d velocityRelative)
        {
            Vector3d R = positionRelative.normalized;
            Vector3d N = Vector3d.Cross(positionRelative, velocityRelative).normalized;
            Vector3d T = Vector3d.Cross(N, R);

            return new Vector3d(
                Vector3d.Dot(relativeVector, T),
                Vector3d.Dot(relativeVector, N),
                Vector3d.Dot(relativeVector, R)
            );
        }

        // Преобразует deltaV из LOCAL(T, N, R) в RELATIVE
        public static Vector3d LocalDeltaVtoRelative(Vector3d deltaVLocal, Vector3d positionRelative, Vector3d velocityRelative)
        {
            Vector3d R = positionRelative.normalized; // радиальная ось
            Vector3d N = Vector3d.Cross(positionRelative, velocityRelative).normalized; // нормаль к орбите
            Vector3d T = Vector3d.Cross(N, R); // тангенциальная ось

            return deltaVLocal.x * T + deltaVLocal.y * N + deltaVLocal.z * R;
        }

    }
}