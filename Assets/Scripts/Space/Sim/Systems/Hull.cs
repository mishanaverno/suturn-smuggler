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

        double heat;

        public HullPanel this[HullSection section] => panels[(int)section];

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
