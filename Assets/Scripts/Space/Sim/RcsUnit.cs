using DoublePrecision;

namespace OuterSpace.Sim
{
    /// <summary>РСУ на холодном метане: сдвигает корабль, не поворачивая его. Тяга по оси фиксированная.</summary>
    public class RcsUnit
    {
        readonly Engine spec;
        readonly Tanks tanks;

        public RcsUnit(Engine spec, Tanks tanks)
        {
            this.spec = spec;
            this.tanks = tanks;
        }

        /// <summary>Команда по связанным осям: вперёд, влево, вверх, каждая в [-1, 1].</summary>
        public Vector3d command;
        /// <summary>Доля тяги, 0…1: положение рычага. Общая на все оси.</summary>
        public double throttle = 1.0;

        /// <summary>
        /// Прирост скорости за dt с расходом метана. Сопла по осям независимы: расход — сумма
        /// по осям, а не по модулю направления.
        /// </summary>
        public Vector3d Fire(Attitude attitude, double shipMass, double dt)
        {
            double axes = Mathd.Abs(command.x) + Mathd.Abs(command.y) + Mathd.Abs(command.z);
            if (axes == 0.0) return Vector3d.zero;

            Vector3d direction = attitude.Forward * command.x + attitude.Left * command.y + attitude.Up * command.z;
            return tanks.Expend(shipMass, direction * (spec.ExhaustVelocity / axes),
                spec.MassFlow * Mathd.Clamp01(throttle) * axes, spec.LoxShare, dt);
        }
    }
}
