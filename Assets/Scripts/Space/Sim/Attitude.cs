using DoublePrecision;

namespace OuterSpace.Sim
{
    /// <summary>
    /// Ориентация корабля в инерциальной системе центрального тела и её изменение реактивной
    /// системой управления.
    ///
    /// Связанные оси: X — вперёд, туда смотрит тяга; Y — влево; Z — вверх, над головой пилота.
    /// Система правая, как и вся симуляция.
    ///
    /// Момента инерции и отдельных двигателей ориентации нет: РСУ задана угловым ускорением и
    /// потолком угловой скорости. Этого хватает, чтобы разворот стоил времени, а корабль по
    /// инерции проворачивался дальше отпущенной ручки.
    /// </summary>
    public class Attitude
    {
        /// <summary>Угловое ускорение РСУ, рад/с².</summary>
        public const double RcsAcceleration = 10.0 * Mathd.Deg2Rad;
        /// <summary>Потолок угловой скорости, рад/с.</summary>
        public const double MaxRate = 20.0 * Mathd.Deg2Rad;
        /// <summary>
        /// Потолок шага интегрирования, с. На перемотке за тик проходят часы, и разворот,
        /// посчитанный явным шагом такой длины, не значил бы ничего. Ориентация на перемотке
        /// почти не меняется: корабль доворачивает в реальном времени.
        /// </summary>
        const double MaxStep = 0.1;

        public Quaterniond rotation = Quaterniond.identity;
        /// <summary>Угловая скорость в инерциальных осях, рад/с.</summary>
        public Vector3d angularVelocity;
        /// <summary>
        /// Вращение, с которым автопилот переносит корабль вслед движущейся опоре. Держится
        /// отдельно от angularVelocity: регулятор ведёт только остаток до опоры.
        /// </summary>
        Vector3d trackingVelocity;

        /// <summary>Полная угловая скорость корабля, рад/с: своя и переносная.</summary>
        public Vector3d Rate => angularVelocity + trackingVelocity;

        /// <summary>Продольная ось корабля: направление тяги.</summary>
        public Vector3d Forward => rotation * Vector3d.right;
        public Vector3d Left => rotation * Vector3d.up;
        public Vector3d Up => rotation * Vector3d.forward;

        /// <summary>Мгновенный разворот: корабль ставится уже направленным, разворачивать его нечем.</summary>
        public void Snap(Vector3d forward)
        {
            if (forward.sqrMagnitude <= 0.0) return;
            Error(forward, out Vector3d axis, out double angle);
            rotation = Quaterniond.AngleAxis(angle * Mathd.Rad2Deg, axis) * rotation;
            angularVelocity = Vector3d.zero;
        }

        /// <summary>
        /// Ручное вращение: команда по связанным осям — крен, тангаж, рыскание, каждая в [-1, 1].
        /// Ручка задаёт ускорение, а не скорость: отпущенная, она вращение не останавливает.
        /// </summary>
        public void Rotate(Vector3d command, double dt)
        {
            dt = Mathd.Min(dt, MaxStep);
            angularVelocity = ClampRate(angularVelocity + rotation * command * (RcsAcceleration * dt));
            Integrate(dt);
        }

        /// <summary>
        /// Автопилот: развернуть тягу в заданное направление и остановиться на нём. Целевая
        /// скорость берётся из тормозного пути — sqrt(2·a·θ), — поэтому разворот выходит на
        /// потолок скорости и гасится ровно к цели. Вблизи цели у корня бесконечная крутизна,
        /// и шаг перелетал бы направление то в одну, то в другую сторону; поэтому скорость
        /// не больше той, что закрывает остаток ровно за шаг.
        /// </summary>
        public void AlignTo(Vector3d forward, double dt)
        {
            if (forward.sqrMagnitude <= 0.0) return;
            dt = Mathd.Min(dt, MaxStep);
            Error(forward, out Vector3d axis, out double angle);
            Vector3d wanted = axis * Mathd.Min(Mathd.Min(MaxRate, Mathd.Sqrt(2.0 * RcsAcceleration * angle)), angle / dt);
            Vector3d change = wanted - angularVelocity;
            double budget = RcsAcceleration * dt;
            if (change.magnitude > budget) change = change.normalized * budget;
            angularVelocity += change;
            Integrate(dt);
        }

        /// <summary>
        /// Перенести корабль вместе с опорным направлением, повернувшимся с from на to.
        /// Разворот к опоре меряется относительно неё, поэтому угловая скорость поворачивается
        /// вместе с кораблём.
        /// </summary>
        public void Carry(Vector3d from, Vector3d to, double dt)
        {
            from = from.normalized;
            to = to.normalized;
            Vector3d axis = Vector3d.Cross(from, to);
            double sin = axis.magnitude;
            if (sin <= 0.0)
            {
                trackingVelocity = Vector3d.zero;
                return;
            }
            double angle = Mathd.Atan2(sin, Vector3d.Dot(from, to));
            Quaterniond turn = Quaterniond.AngleAxis(angle * Mathd.Rad2Deg, axis / sin);
            rotation = turn * rotation;
            angularVelocity = turn * angularVelocity;
            trackingVelocity = axis / sin * (angle / dt);
        }

        /// <summary>
        /// Перенос кончился: корабль и дальше вращается так, как его вели, — вращение
        /// не пропадает оттого, что автопилот перестал за ним следить.
        /// </summary>
        public void Release()
        {
            angularVelocity = ClampRate(angularVelocity + trackingVelocity);
            trackingVelocity = Vector3d.zero;
        }

        /// <summary>
        /// Стабилизация: погасить вращение и этим остановить корабль там, где он сейчас.
        /// Отдельного удержания направления не нужно — возмущений в модели нет, и корабль
        /// с нулевой угловой скоростью смотрит в одну сторону сколько угодно долго.
        /// </summary>
        public void Damp(double dt)
        {
            dt = Mathd.Min(dt, MaxStep);
            double rate = angularVelocity.magnitude;
            if (rate <= 0.0) return;
            double budget = RcsAcceleration * dt;
            angularVelocity = rate > budget ? angularVelocity - angularVelocity / rate * budget : Vector3d.zero;
            Integrate(dt);
        }

        void Integrate(double dt)
        {
            double rate = angularVelocity.magnitude;
            if (rate <= 0.0) return;
            rotation = Quaterniond.AngleAxis(rate * dt * Mathd.Rad2Deg, angularVelocity / rate) * rotation;
        }

        /// <summary>Ось и угол (рад) поворота, совмещающего тягу с заданным направлением.</summary>
        void Error(Vector3d forward, out Vector3d axis, out double angle)
        {
            Vector3d current = Forward;
            Vector3d target = forward.normalized;
            angle = Mathd.Acos(Mathd.Clamp(Vector3d.Dot(current, target), -1.0, 1.0));
            axis = Vector3d.Cross(current, target);
            if (axis.sqrMagnitude > 0.0)
            {
                axis = axis.normalized;
                return;
            }
            // Строго вперёд или строго назад: оси у векторного произведения нет, и при
            // развороте на 180° годится любая перпендикулярная — иначе автопилот встанет.
            axis = Vector3d.Cross(current, Mathd.Abs(current.x) < 0.9 ? Vector3d.right : Vector3d.up).normalized;
        }

        static Vector3d ClampRate(Vector3d rate) =>
            rate.magnitude > MaxRate ? rate.normalized * MaxRate : rate;
    }
}
