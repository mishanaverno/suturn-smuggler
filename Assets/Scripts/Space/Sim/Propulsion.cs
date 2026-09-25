namespace OuterSpace.Sim
{
    /// <summary>Положение галеты двигательной установки. Схема и числа — в DOCS/DEVICES/ENGINE.md.</summary>
    public enum EngineMode
    {
        Idle,
        Cruise,
        Prox,
    }

    public class Engine
    {
        /// <summary>Тяга, Н.</summary>
        public double thrust;
        /// <summary>Удельный импульс, с.</summary>
        public double isp;
        /// <summary>Кислорода на килограмм метана. Ноль — кислород не нужен.</summary>
        public double oxidizerRatio;

        public const double G0 = 9.80665;
        public double ExhaustVelocity => isp * G0;
        /// <summary>Расход на полной тяге, кг/с.</summary>
        public double MassFlow => thrust / ExhaustVelocity;
        public double LoxShare => oxidizerRatio / (1.0 + oxidizerRatio);
    }

    public class Reactor
    {
        /// <summary>Температура на холостом ходу и на полной мощности, К.</summary>
        public double idleTemperature;
        public double fullTemperature;
        /// <summary>Время выхода от холостого хода на полную мощность, с.</summary>
        public double spoolTime;

        public double Temperature(double power) => idleTemperature + power * (fullTemperature - idleTemperature);
    }

    public class Propulsion
    {
        public Engine nuclear;
        /// <summary>ЯРД на полном форсаже: LOX = MAIN.</summary>
        public Engine nuclearLox;
        public Engine chemical;
        public Engine rcs;
        public Reactor reactor;

        /// <summary>
        /// ЯРД с форсажем в доле afterburn от полного. Тяга и расход идут линейно между чистым
        /// ЯРД и полным форсажем, Isp из них следует.
        /// </summary>
        public Engine Afterburning(double afterburn)
        {
            double thrust = nuclear.thrust + afterburn * (nuclearLox.thrust - nuclear.thrust);
            double flow = nuclear.MassFlow + afterburn * (nuclearLox.MassFlow - nuclear.MassFlow);
            return new Engine
            {
                thrust = thrust,
                isp = thrust / (flow * Engine.G0),
                oxidizerRatio = afterburn * nuclearLox.oxidizerRatio,
            };
        }
    }
}
