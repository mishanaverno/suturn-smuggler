using DoublePrecision;
using Game;
using OuterSpace.Sim.Objects;

namespace OuterSpace.Sim
{
    /// <summary>
    /// Положение узла корабля относительно узла станции, в осях узла станции: ось — наружу из
    /// узла, верх — его верх, бок — третья ось правой тройки (бок, верх, ось).
    /// </summary>
    public readonly struct DockingState
    {
        /// <summary>Вдоль оси узла, м; положительная — корабль перед узлом.</summary>
        public readonly double Range;
        public readonly double Side;
        public readonly double Up;
        /// <summary>Скорость сближения по оси, м/с; положительная — корабль идёт к узлу.</summary>
        public readonly double ClosingSpeed;
        public readonly double SideSpeed;
        public readonly double UpSpeed;
        /// <summary>
        /// Рассогласование узлов, градусы. Рысканье и тангаж — куда уведена ось узла корабля
        /// (к боку и к верху), крен — поворот его верха вокруг оси.
        /// </summary>
        public readonly double Roll;
        public readonly double Pitch;
        public readonly double Yaw;

        public DockingState(double range, double side, double up, double closingSpeed, double sideSpeed, double upSpeed,
            double roll, double pitch, double yaw)
        {
            Range = range;
            Side = side;
            Up = up;
            ClosingSpeed = closingSpeed;
            SideSpeed = sideSpeed;
            UpSpeed = upSpeed;
            Roll = roll;
            Pitch = pitch;
            Yaw = yaw;
        }

        public double Lateral => Mathd.Sqrt(Side * Side + Up * Up);
        public double Distance => Mathd.Sqrt(Range * Range + Side * Side + Up * Up);
        /// <summary>Как меняется расстояние между узлами, м/с; минус — сближаются.</summary>
        public double RangeRate => Distance > 0.0 ? (Range * -ClosingSpeed + Side * SideSpeed + Up * UpSpeed) / Distance : 0.0;
        /// <summary>Относительная скорость узлов целиком, м/с.</summary>
        public double Speed => Mathd.Sqrt(ClosingSpeed * ClosingSpeed + SideSpeed * SideSpeed + UpSpeed * UpSpeed);
        /// <summary>Угол между осью узла станции и направлением на узел корабля, градусы.</summary>
        public double OffAxis => Mathd.Atan2(Lateral, Range) * Mathd.Rad2Deg;
    }

    public static class DockingGeometry
    {
        public static DockingState Measure(Ship ship, Station station) => Measure(
            ship.simTransform.GLOBAL_R, ship.simTransform.GLOBAL_V, ship.attitude.rotation, ship.attitude.Rate, ship.ActivePort,
            station.simTransform.GLOBAL_R, station.simTransform.GLOBAL_V, station.port);

        /// <param name="shipRotation">Связанные оси корабля в инерциальные.</param>
        /// <param name="shipRate">Угловая скорость корабля в инерциальных осях, рад/с.</param>
        /// <param name="shipPort">Узел корабля в связанных осях.</param>
        /// <param name="stationPort">Узел станции в осях симуляции: станция не вращается.</param>
        public static DockingState Measure(Vector3d shipR, Vector3d shipV, Quaterniond shipRotation, Vector3d shipRate, PortData shipPort,
            Vector3d stationR, Vector3d stationV, PortData stationPort)
        {
            Vector3d axis = stationPort.axis;
            Vector3d up = stationPort.up;
            Vector3d side = Vector3d.Cross(up, axis);

            // Узел корабля в стороне от центра масс, и вращение корабля его переносит.
            Vector3d arm = shipRotation * shipPort.position;
            Vector3d offset = shipR + arm - (stationR + stationPort.position);
            Vector3d relative = shipV + Vector3d.Cross(shipRate, arm) - stationV;

            // Совмещённые узлы смотрят друг на друга: ось корабля против оси станции.
            Vector3d facing = -(shipRotation * shipPort.axis);
            Vector3d shipUp = shipRotation * shipPort.up;

            return new DockingState(
                Vector3d.Dot(offset, axis),
                Vector3d.Dot(offset, side),
                Vector3d.Dot(offset, up),
                -Vector3d.Dot(relative, axis),
                Vector3d.Dot(relative, side),
                Vector3d.Dot(relative, up),
                Degrees(Vector3d.Dot(shipUp, side), Vector3d.Dot(shipUp, up)),
                Degrees(Vector3d.Dot(facing, up), Vector3d.Dot(facing, axis)),
                Degrees(Vector3d.Dot(facing, side), Vector3d.Dot(facing, axis)));
        }

        /// <summary>Ориентация корабля, в которой его узел смотрит в узел станции и верх к верху.</summary>
        public static Quaterniond Aligned(PortData shipPort, PortData stationPort) =>
            Quaterniond.LookRotation(-stationPort.axis, stationPort.up)
            * Quaterniond.Inverse(Quaterniond.LookRotation(shipPort.axis, shipPort.up));

        static double Degrees(double y, double x) => Mathd.Atan2(y, x) * Mathd.Rad2Deg;
    }
}
