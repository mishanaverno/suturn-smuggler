using System.Collections.Generic;
using DoublePrecision;

namespace OuterSpace.Sim.Systems
{
    /// <summary>Что идёт по связи между узлами. Тепло, среды и данные встанут сюда же.</summary>
    public enum Flow
    {
        Wear,
    }

    public readonly struct Link
    {
        public readonly SystemNode to;
        public readonly Flow flow;

        public Link(SystemNode to, Flow flow)
        {
            this.to = to;
            this.flow = flow;
        }
    }

    /// <summary>
    /// Узел графа систем: агрегат корабля или внешний источник вроде пыли. Узел отдаёт по своим
    /// связям в тик систем и принимает то, что отдали ему. Знать, кто на другом конце связи,
    /// ему не нужно — только вид потока.
    /// </summary>
    public abstract class SystemNode
    {
        /// <summary>Износ, доля: 0 — новый, 1 — изношен полностью.</summary>
        public double wear;
        public readonly List<Link> outputs = new();

        /// <summary>Отдать по связям за dt симуляционного времени.</summary>
        public virtual void Tick(double dt) { }

        public virtual void Accept(Flow flow, double amount)
        {
            if (flow == Flow.Wear) wear = Mathd.Min(wear + amount, 1.0);
        }
    }
}
