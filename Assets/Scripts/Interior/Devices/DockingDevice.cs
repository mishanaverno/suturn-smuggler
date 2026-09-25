using System;
using OuterSpace.Sim;

namespace Interior
{
    /// <summary>
    /// Стыковка: выбор узла по борту, удержание ориентации по маяку станции и показания узла.
    /// Всё, что делается уже у станции — заправка, расстыковка, — не здесь, а в её меню.
    /// </summary>
    public class DockingDevice : ShipDevice
    {
        protected override void Wire()
        {
            Bind(CommandId.BeaconHold, () => Ship.SetBeaconHold(!Ship.BeaconHolding),
                () => Ship != null && (Ship.BeaconLocked || Ship.BeaconHolding));
            // Узел выбирается до захвата: пристыкованный корабль держится тем, которым подошёл.
            Bind(CommandId.DockingPortLeft, () => Ship.rightPortActive = false, Undocked);
            Bind(CommandId.DockingPortRight, () => Ship.rightPortActive = true, Undocked);

            Bind(SignalId.BeaconLocked, () => Ship != null && Ship.BeaconLocked);
            Bind(SignalId.BeaconHolding, () => Ship != null && Ship.BeaconHolding);
            Bind(SignalId.Docked, () => Ship != null && Ship.DockedTo != null);
            Bind(SignalId.DockingPortLeft, () => Ship != null && !Ship.rightPortActive);
            Bind(SignalId.DockingPortRight, () => Ship != null && Ship.rightPortActive);

            Bind(ReadingId.DockingRange, () => Read(d => d.Range));
            Bind(ReadingId.DockingClosingSpeed, () => Read(d => d.ClosingSpeed));
            Bind(ReadingId.DockingSide, () => Read(d => d.Side));
            Bind(ReadingId.DockingUp, () => Read(d => d.Up));
            Bind(ReadingId.DockingSideSpeed, () => Read(d => d.SideSpeed));
            Bind(ReadingId.DockingUpSpeed, () => Read(d => d.UpSpeed));
            Bind(ReadingId.DockingRoll, () => Read(d => d.Roll));
            Bind(ReadingId.DockingPitch, () => Read(d => d.Pitch));
            Bind(ReadingId.DockingYaw, () => Read(d => d.Yaw));
        }

        static bool Undocked() => Ship != null && Ship.DockedTo == null;

        /// <summary>Нет станции-цели — табло гаснет, а не показывает ноль.</summary>
        static double Read(Func<DockingState, double> value) =>
            Ship?.Docking is DockingState state ? value(state) : double.NaN;
    }
}
