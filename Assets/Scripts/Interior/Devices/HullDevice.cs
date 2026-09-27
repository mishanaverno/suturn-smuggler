using OuterSpace.Sim.Systems;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Корпус: износ шести панелей и температура. Последствий у износа пока нет — только
    /// показания. Числа ниже — настройка механики, а не паспорт корабля, как у реактора.
    /// </summary>
    public class HullDevice : ShipDevice
    {
        const double SecondsPerDay = 86400.0;

        [Tooltip("За сколько суток пыль полностью изнашивает панель, стоящую прямо в потоке.")]
        public double fullWearDays = 365.0;

        [Tooltip("Температура корпуса без подогрева реактором, К.")]
        public double spaceTemperature = 90.0;
        [Tooltip("На сколько кельвин поднимает равновесие корпуса излишек реактора в одну долю полной мощности.")]
        public double heatGain = 200.0;
        [Tooltip("Тепловая постоянная корпуса, с. Больше — медленнее и нагрев, и остывание.")]
        public double coolTime = 600.0;
        [Tooltip("Температура, выше которой корпус изнашивается, К.")]
        public double overheatLimit = 350.0;
        [Tooltip("За сколько суток перегрев на 100 К сверх предела полностью изнашивает панели.")]
        public double overheatDays = 7.0;

        // В Start, как у двигателя: корабль уже построен, первый тик ещё не прошёл.
        void Start()
        {
            Ship.dust.rate = 1.0 / (fullWearDays * SecondsPerDay);

            Hull hull = Ship.hull;
            hull.spaceTemperature = spaceTemperature;
            hull.temperature = spaceTemperature;
            hull.heatGain = heatGain;
            hull.coolTime = coolTime;
            hull.overheatLimit = overheatLimit;
            hull.overheatRate = 1.0 / (overheatDays * SecondsPerDay * 100.0);
        }

        protected override void Wire()
        {
            BindWear(ReadingId.HullNose, HullSection.Nose);
            BindWear(ReadingId.HullTail, HullSection.Tail);
            BindWear(ReadingId.HullLeft, HullSection.Left);
            BindWear(ReadingId.HullRight, HullSection.Right);
            BindWear(ReadingId.HullTop, HullSection.Top);
            BindWear(ReadingId.HullBottom, HullSection.Bottom);
            Bind(ReadingId.HullTemperature, () => Ship == null ? double.NaN : Ship.hull.temperature);
        }

        void BindWear(ReadingId id, HullSection section) =>
            Bind(id, () => Ship == null ? double.NaN : Ship.hull[section].wear);
    }
}
