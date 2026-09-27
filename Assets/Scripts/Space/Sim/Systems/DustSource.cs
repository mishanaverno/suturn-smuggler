using System;
using DoublePrecision;

namespace OuterSpace.Sim.Systems
{
    /// <summary>
    /// Пыль и микрочастицы: фоновый износ корпуса. Узел внешний — не агрегат корабля, а среда,
    /// но в графе стоит наравне с агрегатами и отдаёт износ по своим связям.
    ///
    /// Поток идёт навстречу скорости корабля относительно центрального тела. Панель, стоящая
    /// прямо в потоке, получает полный темп, стоящая боком — ничего, наискось — долю по
    /// косинусу. От модуля скорости износ не зависит.
    /// </summary>
    public class DustSource : SystemNode
    {
        /// <summary>Износ панели, стоящей прямо в потоке, долей в секунду.</summary>
        public double rate;

        readonly Attitude attitude;
        readonly Func<Vector3d> velocity;

        public DustSource(Attitude attitude, Func<Vector3d> velocity)
        {
            this.attitude = attitude;
            this.velocity = velocity;
        }

        public override void Emit(double dt)
        {
            Vector3d v = velocity();
            if (v.sqrMagnitude == 0.0) return;

            Vector3d heading = v.normalized;
            foreach (Link link in outputs)
            {
                if (link.flow != Flow.Wear || link.to is not HullPanel panel) continue;
                double share = Vector3d.Dot(attitude.rotation * panel.normal, heading);
                if (share > 0.0) panel.Accept(Flow.Wear, rate * dt * share);
            }
        }
    }
}
