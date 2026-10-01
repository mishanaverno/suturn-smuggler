using DoublePrecision;

namespace OuterSpace.Sim
{
    /// <summary>
    /// Оси поверхности тела и её вращение — те же, что у тела за иллюминатором
    /// (ExteriorView.Spin): точка, неподвижная в этих осях, неподвижна и на нарисованной
    /// поверхности. В них живёт севший корабль.
    ///
    /// Захваченное тело повёрнуто к центральному одной стороной: оси задают направление на
    /// центральное тело и нормаль орбиты, а угловая скорость — скорость поворота радиус-вектора.
    /// Остальные вращаются вокруг полюса Сатурна. Знак угла — как у вида: сцена зеркальна
    /// симуляции (ExteriorView.ToHull), и поворот на +θ в сцене здесь поворот на −θ.
    /// </summary>
    public static class Surface
    {
        public static Quaterniond Frame(SpaceObject body, double epoch)
        {
            if (body.tidallyLocked)
            {
                Vector3d r = body.simTransform.RELATIVE_R;
                return Quaterniond.LookRotation(-r, Vector3d.Cross(r, body.simTransform.RELATIVE_V));
            }
            if (body.rotationPeriod == 0.0) return Quaterniond.identity;
            // Доля оборота — в double: эпоха в миллионах секунд, а угол нужен точнее градуса.
            return Quaterniond.AngleAxis(-360.0 * (epoch / body.rotationPeriod % 1.0), Vector3d.forward);
        }

        /// <summary>Угловая скорость поверхности в осях симуляции, рад/с.</summary>
        public static Vector3d Spin(SpaceObject body)
        {
            if (body.tidallyLocked)
            {
                Vector3d r = body.simTransform.RELATIVE_R;
                return Vector3d.Cross(r, body.simTransform.RELATIVE_V) / r.sqrMagnitude;
            }
            if (body.rotationPeriod == 0.0) return Vector3d.zero;
            return -Vector3d.forward * (2.0 * System.Math.PI / body.rotationPeriod);
        }

        /// <summary>Скорость точки поверхности r (относительно центра тела), м/с.</summary>
        public static Vector3d Velocity(SpaceObject body, Vector3d r) => Vector3d.Cross(Spin(body), r);
    }
}
