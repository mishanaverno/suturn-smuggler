using DoublePrecision;

namespace OuterSpace.Sim.Systems
{
    public enum HullSection
    {
        Nose,
        Tail,
        Left,
        Right,
        Top,
        Bottom,
    }

    public class HullPanel : SystemNode
    {
        /// <summary>Внешняя нормаль в связанных осях: X — вперёд, Y — влево, Z — вверх.</summary>
        public readonly Vector3d normal;

        public HullPanel(Vector3d normal) => this.normal = normal;
    }

    /// <summary>
    /// Корпус — шесть панелей по граням связанных осей, каждая изнашивается сама. Тепло корпус
    /// принимает целиком, а не по панелям: температура у него одна.
    ///
    /// Температура идёт к равновесию, которое задаёт принятая мощность, и остывает излучением
    /// в космос: dT/dt = [T₀ + gain·q − T] / τ. Шаг — точное решение при постоянной за тик
    /// мощности, поэтому перемотка его не раскачивает. Выше предела перегрев изнашивает все
    /// панели разом — по связям Wear, как и пыль.
    /// </summary>
    public class Hull : SystemNode
    {
        public readonly HullPanel[] panels =
        {
            new(Vector3d.right),
            new(-Vector3d.right),
            new(Vector3d.up),
            new(-Vector3d.up),
            new(Vector3d.forward),
            new(-Vector3d.forward),
        };

        /// <summary>Температура корпуса, К.</summary>
        public double temperature;
        /// <summary>Равновесная температура без подогрева, К.</summary>
        public double spaceTemperature;
        /// <summary>На сколько кельвин поднимает равновесие единица принятой мощности.</summary>
        public double heatGain;
        /// <summary>Тепловая постоянная корпуса, с.</summary>
        public double coolTime;
        /// <summary>Температура, выше которой корпус изнашивается, К.</summary>
        public double overheatLimit;
        /// <summary>Износ каждой панели в долю за секунду на кельвин сверх предела.</summary>
        public double overheatRate;
        /// <summary>Скорость касания поверхности, до которой посадка обходится без износа, м/с.</summary>
        public double landingSpeed;
        /// <summary>Скорость касания поверхности, выше которой корабль разбивается, м/с.</summary>
        public double crashSpeed;
        /// <summary>Износ панели, коснувшейся поверхности на скорости crashSpeed. Между landingSpeed и crashSpeed — по прямой.</summary>
        public double landingWear;
        /// <summary>Скорость удара о станцию, до которой корабль не изнашивается и не отскакивает, м/с.</summary>
        public double bumpSpeed;
        /// <summary>Скорость удара о станцию, выше которой корабль разбивается, м/с.</summary>
        public double rammingSpeed;
        /// <summary>Износ панели, ударившей станцию на скорости rammingSpeed. Между bumpSpeed и rammingSpeed — по прямой.</summary>
        public double bumpWear;
        /// <summary>Доля скорости удара, с которой корабль отскакивает от станции.</summary>
        public double restitution;

        double heat;

        public HullPanel this[HullSection section] => panels[(int)section];

        /// <summary>Панель, чья внешняя нормаль ближе всех к направлению (в связанных осях).</summary>
        public HullPanel Facing(Vector3d direction)
        {
            HullPanel facing = panels[0];
            foreach (HullPanel panel in panels)
                if (Vector3d.Dot(panel.normal, direction) > Vector3d.Dot(facing.normal, direction)) facing = panel;
            return facing;
        }

        /// <summary>Износ от удара: ноль до safe, wearAtFatal на fatal, между ними — по прямой.</summary>
        public static double ImpactWear(double speed, double safe, double fatal, double wearAtFatal) =>
            speed <= safe ? 0.0 : wearAtFatal * (speed - safe) / (fatal - safe);

        public override void Accept(Flow flow, double amount)
        {
            if (flow == Flow.Heat) heat += amount;
            else base.Accept(flow, amount);
        }

        public override void Settle(double dt)
        {
            double equilibrium = spaceTemperature + heatGain * heat / dt;
            heat = 0.0;
            double start = temperature;
            temperature = equilibrium + (start - equilibrium) * Mathd.Exp(-dt / coolTime);

            // Средняя за шаг, а не конечная: на перемотке шаг длиннее тепловой постоянной.
            double over = 0.5 * (start + temperature) - overheatLimit;
            if (over <= 0.0) return;
            foreach (Link link in outputs)
                if (link.flow == Flow.Wear) link.to.Accept(Flow.Wear, overheatRate * over * dt);
        }
    }
}
