using System.Collections.Generic;

namespace OuterSpace.Sim.Systems
{
    /// <summary>
    /// Граф систем корабля и его тик.
    ///
    /// Износ, тепло и среды меняются за секунды, а не за кадр, поэтому граф тикает раз в
    /// секунду симуляционного времени. На перемотке один FixedUpdate покрывает больше секунды,
    /// и тик выходит один на FixedUpdate с полным прошедшим временем: цена не растёт вместе с
    /// перемоткой. Узел, которому крупный шаг вреден, решает свою задачу за dt точно, а не
    /// шагом Эйлера.
    /// </summary>
    public class ShipSystems
    {
        public const double Interval = 1.0;

        readonly List<SystemNode> nodes = new();
        // Первый вызов только отмечает время: эпоха игры не начинается с нуля.
        double tickEpoch = double.NaN;

        public void Add(SystemNode node) => nodes.Add(node);

        public void Connect(SystemNode from, SystemNode to, Flow flow) => from.outputs.Add(new Link(to, flow));

        public void Update(double epoch)
        {
            if (double.IsNaN(tickEpoch)) tickEpoch = epoch;
            double dt = epoch - tickEpoch;
            if (dt < Interval) return;

            tickEpoch = epoch;
            foreach (SystemNode node in nodes) node.Emit(dt);
            foreach (SystemNode node in nodes) node.Settle(dt);
        }

        /// <summary>Пропустить время без тика: стоянка у станции не должна прийти после неё разом.</summary>
        public void Skip(double epoch) => tickEpoch = epoch;
    }
}
