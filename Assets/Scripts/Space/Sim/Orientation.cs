using DoublePrecision;

namespace OuterSpace.Sim
{
    public enum ShipOrientation
    {
        Prograde, Retrograde,
        Normal, Antinormal,
        RadialOut, RadialIn,
        Target, AntiTarget,
        Maneuver,
        Hold,
        Free
    }

    /// <summary>
    /// Направление тяги корабля в инерциальной (RELATIVE) системе центрального тела.
    ///
    /// Нулевой вектор здесь значит «направления нет»: режим требует цели или манёвра, которых
    /// сейчас не существует, либо это Free или Hold. Корабль в таком случае удерживает прежнее
    /// направление — разворачивать его в произвольную сторону из-за снятой цели нельзя.
    /// </summary>
    public static class Orientation
    {
        public static Vector3d Direction(
            ShipOrientation mode,
            Vector3d position,
            Vector3d velocity,
            Vector3d closingVelocity,
            Vector3d maneuverDeltaV)
        {
            switch (mode)
            {
                case ShipOrientation.Prograde: return velocity.normalized;
                case ShipOrientation.Retrograde: return -velocity.normalized;
                case ShipOrientation.Normal: return Vector3d.Cross(position, velocity).normalized;
                case ShipOrientation.Antinormal: return -Vector3d.Cross(position, velocity).normalized;
                case ShipOrientation.RadialOut: return position.normalized;
                case ShipOrientation.RadialIn: return -position.normalized;
                case ShipOrientation.Target: return closingVelocity.normalized;
                case ShipOrientation.AntiTarget: return -closingVelocity.normalized;
                case ShipOrientation.Maneuver: return maneuverDeltaV.normalized;
                default: return Vector3d.zero;
            }
        }
    }
}
