using System.Collections.Generic;
using DoublePrecision;

namespace OuterSpace.Sim.Systems
{
    /// <summary>Что идёт по связи между узлами. Тепло, среды и данные встанут сюда же.</summary>
    public enum Flow
    {
        Wear,
        Heat,
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
    ///
    /// Тик в две фазы: сначала все узлы отдают, потом все усваивают принятое. Иначе результат
    /// зависел бы от порядка узлов — принятое после своего хода доходило бы тиком позже и
    /// считалось бы с чужим dt.
    /// </summary>
    public abstract class SystemNode
    {
        /// <summary>Износ, доля: 0 — новый, 1 — изношен полностью.</summary>
        public double wear;
        public readonly List<Link> outputs = new();

        /// <summary>Отдать по связям за dt симуляционного времени.</summary>
        public virtual void Emit(double dt) { }

        /// <summary>Усвоить принятое за тик: все узлы уже отдали.</summary>
        public virtual void Settle(double dt) { }

        public virtual void Accept(Flow flow, double amount)
        {
            if (flow == Flow.Wear) wear = Mathd.Min(wear + amount, 1.0);
        }
    }
}
