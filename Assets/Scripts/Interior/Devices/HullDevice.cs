using OuterSpace.Sim.Systems;
using UnityEngine;

namespace Interior
{
    /// <summary>Корпус: износ шести панелей. Последствий у износа пока нет — только показания.</summary>
    public class HullDevice : ShipDevice
    {
        const double SecondsPerDay = 86400.0;

        [Tooltip("За сколько суток пыль полностью изнашивает панель, стоящую прямо в потоке.")]
        public double fullWearDays = 365.0;

        // В Start, как у двигателя: корабль уже построен, первый тик ещё не прошёл.
        void Start()
        {
            Ship.dust.rate = 1.0 / (fullWearDays * SecondsPerDay);
        }

        protected override void Wire()
        {
            BindWear(ReadingId.HullNose, HullSection.Nose);
            BindWear(ReadingId.HullTail, HullSection.Tail);
            BindWear(ReadingId.HullLeft, HullSection.Left);
            BindWear(ReadingId.HullRight, HullSection.Right);
            BindWear(ReadingId.HullTop, HullSection.Top);
            BindWear(ReadingId.HullBottom, HullSection.Bottom);
        }

        void BindWear(ReadingId id, HullSection section) =>
            Bind(id, () => Ship == null ? double.NaN : Ship.hull[section].wear);
    }
}
