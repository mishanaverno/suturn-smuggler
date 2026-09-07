using DoublePrecision;
using System.Collections.Generic;

namespace OuterSpace.Sim
{
    public static class SOITransition
    {
        // Порог выхода из сферы влияния больше порога входа: без этого объект,
        // идущий ровно по границе, менял бы родителя каждый тик и каждый тик
        // пересчитывал элементы орбиты с новой эпохой, накапливая ошибку.
        public const double Hysteresis = 0.01;

        // Объект может за один тик на большой перемотке пересечь несколько границ,
        // но цепочка вложенных сфер влияния заведомо короче этого предела.
        const int MaxSteps = 8;

        public static SpaceObject ResolveCentralBody(SpaceObject obj, SpaceObject currentCentral, IReadOnlyList<SpaceObject> allBodies, double hysteresis)
        {
            SpaceObject central = currentCentral;
            for (int step = 0; step < MaxSteps; step++)
            {
                SpaceObject next = Descend(obj, central, allBodies) ?? Ascend(obj, central, hysteresis);
                if (next == null) return central;
                central = next;
            }
            return central;
        }

        static SpaceObject Descend(SpaceObject obj, SpaceObject central, IReadOnlyList<SpaceObject> allBodies)
        {
            SpaceObject best = null;
            double bestRatio = 1.0;
            for (int i = 0; i < allBodies.Count; i++)
            {
                SpaceObject candidate = allBodies[i];
                if (candidate == obj || candidate.centralBody != central) continue;
                double ratio = Distance(obj, candidate) / candidate.SOI;
                if (ratio >= bestRatio) continue;
                bestRatio = ratio;
                best = candidate;
            }
            return best;
        }

        static SpaceObject Ascend(SpaceObject obj, SpaceObject central, double hysteresis)
        {
            if (central.IsStar) return null;
            return Distance(obj, central) > central.SOI * (1.0 + hysteresis) ? central.centralBody : null;
        }

        static double Distance(SpaceObject a, SpaceObject b)
        {
            return Vector3d.Distance(a.simTransform.GLOBAL_R, b.simTransform.GLOBAL_R);
        }

        /// <summary>
        /// Переводит объект под новое центральное тело, не меняя его абсолютных
        /// положения и скорости: GLOBAL_R / GLOBAL_V остаются как есть, а
        /// относительные величины и элементы орбиты пересчитываются от нового родителя.
        /// </summary>
        public static void ChangeCentralBody(SpaceObject obj, SpaceObject newCentral, double epoch)
        {
            SpaceObject previous = obj.centralBody;
            obj.SetCentralBody(newCentral);
            obj.orbitParams = AstroDynamic.CalculateOrbitElements(
                obj.simTransform.RELATIVE_R,
                obj.simTransform.RELATIVE_V,
                newCentral.MU,
                epoch
            );
            obj.CalculateSOI();
            obj.OnCentralBodyChanged(previous);
        }
    }
}
