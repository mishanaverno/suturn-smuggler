using OuterSpace.Sim;

namespace Interior
{
    /// <summary>
    /// Система ориентации: куда автопилот держит нос корабля.
    ///
    /// Режим набирается двумя органами — осью и знаком, — а не отдельной кнопкой на каждый:
    /// режимы парные, и пара «ось + знак» ложится на две галеты вместо десяти кнопок.
    /// Ось и знак хранятся здесь, а не в корабле: это положение ручек, а корабль знает
    /// только итоговый режим.
    ///
    /// Оси — связанные оси корабля в режиме програды: X — вперёд по скорости, Y — нормаль,
    /// Z — радиально наружу. T и M осями не являются, это «цель» и «манёвр»; минус у M —
    /// свободный режим, потому что антиманёвр никому не нужен.
    /// </summary>
    public class SasDevice : ShipDevice
    {
        enum Axis { X, Y, Z, T, M }

        // Ставятся так, чтобы вместе давать Free: корабль стартует без автопилота.
        Axis axis = Axis.M;
        bool plus;

        protected override void Wire()
        {
            Bind(CommandId.SasAxisX, () => Set(Axis.X, plus), HasShip);
            Bind(CommandId.SasAxisY, () => Set(Axis.Y, plus), HasShip);
            Bind(CommandId.SasAxisZ, () => Set(Axis.Z, plus), HasShip);
            Bind(CommandId.SasAxisT, () => Set(Axis.T, plus), HasShip);
            Bind(CommandId.SasAxisM, () => Set(Axis.M, plus), HasShip);
            Bind(CommandId.SasDirPlus, () => Set(axis, true), HasShip);
            Bind(CommandId.SasDirMinus, () => Set(axis, false), HasShip);
        }

        static bool HasShip() => Ship != null;

        void Set(Axis newAxis, bool newPlus)
        {
            axis = newAxis;
            plus = newPlus;
            Ship.orientation = Mode();
        }

        ShipOrientation Mode() => axis switch
        {
            Axis.X => plus ? ShipOrientation.Prograde : ShipOrientation.Retrograde,
            Axis.Y => plus ? ShipOrientation.Normal : ShipOrientation.Antinormal,
            Axis.Z => plus ? ShipOrientation.RadialOut : ShipOrientation.RadialIn,
            Axis.T => plus ? ShipOrientation.Target : ShipOrientation.AntiTarget,
            _ => plus ? ShipOrientation.Maneuver : ShipOrientation.Free,
        };
    }
}
