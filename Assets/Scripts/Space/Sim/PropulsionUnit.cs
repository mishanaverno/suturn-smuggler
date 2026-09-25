using DoublePrecision;

namespace OuterSpace.Sim
{
    /// <summary>
    /// Маршевая установка: галета, двойной РУД, реактор и расход. Зажигание — у корабля: от него
    /// зависят прогноз и перемотка, а установка только говорит, можно ли зажечь. Схема и
    /// числа — в DOCS/DEVICES/ENGINE.md.
    /// </summary>
    public class PropulsionUnit
    {
        public readonly Propulsion spec;
        readonly Tanks tanks;

        public PropulsionUnit(Propulsion spec, Tanks tanks)
        {
            this.spec = spec;
            this.tanks = tanks;
        }

        public EngineMode Mode { get; private set; } = EngineMode.Idle;
        /// <summary>Мощность реактора, 0…1. Идёт за заказом РУД с задержкой, см. UpdateReactor.</summary>
        public double reactorPower;
        double reactorEpoch;

        /// <summary>
        /// Двойной РУД, 0…1. MAIN — заказ мощности реактора в CRUISE и тяга ЖРД в PROX, LOX —
        /// подача кислорода. Расчётное ускорение остаётся паспортным — по нему считаются
        /// длительность прожига и момент его начала, то есть план. Дросселирование меняет
        /// исполнение плана, а не сам план: прожиг на половине тяги уйдёт за расчётное окно, и
        /// это видно по остатку.
        ///
        /// Рукоятки толкают друг друга: MAIN не бывает ниже LOX. В PROX, коснувшись, они
        /// сцепляются и дальше ходят только вместе — в обе стороны.
        /// </summary>
        public double MainThrottle { get; private set; }
        public double LoxThrottle { get; private set; }
        bool throttlesLatched;

        /// <summary>На каком расстоянии рукоятки считаются касающимися, доля хода.</summary>
        const double ThrottleContact = 0.01;

        public void SetMainThrottle(double value)
        {
            MainThrottle = Mathd.Clamp01(value);
            if (throttlesLatched || LoxThrottle > MainThrottle) LoxThrottle = MainThrottle;
            LatchOnContact();
        }

        public void SetLoxThrottle(double value)
        {
            LoxThrottle = Mathd.Clamp01(value);
            if (throttlesLatched || MainThrottle < LoxThrottle) MainThrottle = LoxThrottle;
            LatchOnContact();
        }

        /// <summary>Вне PROX сцепка снимается; в PROX рукоятки сцепляются, только если уже касаются.</summary>
        public void SetMode(EngineMode mode)
        {
            Mode = mode;
            throttlesLatched = false;
            LatchOnContact();
        }

        void LatchOnContact()
        {
            if (Mode != EngineMode.Prox || MainThrottle - LoxThrottle > ThrottleContact) return;
            throttlesLatched = true;
            LoxThrottle = MainThrottle;
        }

        /// <summary>
        /// В IDLE зажигать нечего. В PROX с разведёнными рукоятками смесь не та — факел не
        /// загорится, пока они не сойдутся и не сцепятся.
        /// </summary>
        public bool CanIgnite => HasPropellant && Mode switch
        {
            EngineMode.Idle => false,
            EngineMode.Prox => throttlesLatched,
            _ => true,
        };

        public bool HasPropellant => tanks.Usable(Engine.LoxShare) > 0.0;

        /// <summary>Доля LOX от MAIN, 0…1. Соотношение смеси — она, умноженная на O/F режима.</summary>
        double Mixture => MainThrottle > 0.0 ? LoxThrottle / MainThrottle : 0.0;

        /// <summary>
        /// Паспорт текущего режима на полной мощности. IDLE тяги не даёт, но план считается по
        /// ЯРД — в CRUISE его и исполнят.
        /// </summary>
        public Engine Engine => Mode == EngineMode.Prox ? spec.chemical : spec.Afterburning(Mixture);

        public double ReactorTemperature => spec.reactor.Temperature(reactorPower);

        /// <summary>Тяга при зажигании, Н: в CRUISE её даёт реактор, в PROX — MAIN.</summary>
        public double Thrust => Engine.thrust * Mode switch
        {
            EngineMode.Cruise => reactorPower,
            EngineMode.Prox => MainThrottle,
            _ => 0.0,
        };

        /// <summary>
        /// Удельный импульс с учётом разогрева, с. Пока реактор холоднее, чем требует
        /// заказанная мощность, скорость истечения ниже паспортной в √(T / T_заказа) раз. На
        /// установившейся мощности штрафа нет — и план, посчитанный по паспорту, для
        /// прогретого реактора точен.
        /// </summary>
        public double SpecificImpulse
        {
            get
            {
                if (Mode != EngineMode.Cruise) return Engine.isp;
                double warmup = Mathd.Sqrt(ReactorTemperature / spec.reactor.Temperature(MainThrottle));
                return Engine.isp * Mathd.Min(1.0, warmup);
            }
        }

        /// <summary>
        /// Реактор идёт к заказанной мощности с постоянной скоростью: от холостого хода до
        /// полной за spoolTime. Заказ — MAIN в CRUISE; в IDLE и PROX реактор уходит на холостой
        /// ход. Время симуляционное, как у тяги.
        /// </summary>
        public void UpdateReactor(double epoch)
        {
            double dt = epoch - reactorEpoch;
            reactorEpoch = epoch;
            if (dt <= 0.0) return;

            double demand = Mode == EngineMode.Cruise ? MainThrottle : 0.0;
            double step = dt / spec.reactor.spoolTime;
            reactorPower = Mathd.Clamp(demand, reactorPower - step, reactorPower + step);
        }

        /// <summary>Прирост скорости за dt вдоль forward с расходом топлива.</summary>
        public Vector3d Burn(Vector3d forward, double shipMass, double dt)
        {
            double exhaust = SpecificImpulse * Engine.G0;
            return tanks.Expend(shipMass, forward * exhaust, Thrust / exhaust, Engine.LoxShare, dt);
        }

        /// <summary>Время набора deltaV на полной тяге текущего режима, по Циолковскому, с.</summary>
        public double BurnDuration(double shipMass, double deltaV) =>
            shipMass / Engine.MassFlow * (1.0 - Mathd.Exp(-deltaV / Engine.ExhaustVelocity));

        /// <summary>Характеристическая скорость, которую даст остаток топлива в текущем режиме, м/с.</summary>
        public double AvailableDeltaV(double shipMass) =>
            Engine.ExhaustVelocity * Mathd.Log(shipMass / (shipMass - tanks.Usable(Engine.LoxShare)));
    }
}
