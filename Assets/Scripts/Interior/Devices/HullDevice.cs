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

        [Header("Landing")]
        [Tooltip("Скорость касания поверхности, до которой посадка обходится без износа, м/с.")]
        public double landingSpeed = 2.0;
        [Tooltip("Скорость касания, выше которой корабль разбивается, м/с.")]
        public double crashSpeed = 10.0;
        [Tooltip("Износ панели, коснувшейся поверхности на скорости разрушения. Между двумя скоростями — по прямой.")]
        public double landingWear = 0.5;

        [Header("Station collisions")]
        [Tooltip("Скорость удара о станцию, до которой корабль не изнашивается и не отскакивает, м/с.")]
        public double bumpSpeed = 0.5;
        [Tooltip("Скорость удара о станцию, выше которой корабль разбивается, м/с.")]
        public double rammingSpeed = 5.0;
        [Tooltip("Износ панели, ударившей станцию на скорости разрушения. Между двумя скоростями — по прямой.")]
        public double bumpWear = 0.5;
        [Tooltip("Доля скорости удара, с которой корабль отскакивает от станции.")]
        public double restitution = 0.3;

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
            hull.landingSpeed = landingSpeed;
            hull.crashSpeed = crashSpeed;
            hull.landingWear = landingWear;
            hull.bumpSpeed = bumpSpeed;
            hull.rammingSpeed = rammingSpeed;
            hull.bumpWear = bumpWear;
            hull.restitution = restitution;
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
