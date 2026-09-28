using System.Collections.Generic;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;

namespace Game
{
    /// <summary>
    /// Все моменты, к которым игроку надо быть у приборов, одним списком по времени: таймеры,
    /// начало прожига и узел ближайшего манёвра, смены сферы влияния и сближения с целью на
    /// фактической траектории. События плановых траекторий сюда не входят: корабль на них
    /// ещё не находится, и до них может не дойти. Его показывает экран часов, и по нему же останавливается
    /// перемотка: что видно в списке, то и не проспишь, и наоборот.
    /// </summary>
    public static class Schedule
    {
        public enum Kind { Timer, Burn, Node, Soi, Impact, Approach }

        public readonly struct Entry
        {
            public readonly double Epoch;
            public readonly string Label;
            public readonly Kind Kind;
            /// <summary>0 — строка ближайшего манёвра; -1 — фактическая траектория или таймер.</summary>
            public readonly int Maneuver;
            public readonly Timers.Timer Timer;

            public Entry(double epoch, string label, Kind kind, int maneuver = -1, Timers.Timer timer = null)
            {
                Epoch = epoch;
                Label = label;
                Kind = kind;
                Maneuver = maneuver;
                Timer = timer;
            }
        }

        public static void Collect(Ship ship, Timers timers, double now, List<Entry> into)
        {
            into.Clear();
            if (timers != null)
            {
                foreach (Timers.Timer timer in timers.List)
                    into.Add(new Entry(timer.Epoch, $"T{timer.Number}", Kind.Timer, timer: timer));
            }

            if (ship != null)
            {
                AddTrajectory(ship.trajectory, now, into);
                // Из плана — только ближайший узел: он один лежит на фактической траектории.
                // Прожиг центрируется на узле, поэтому начинается раньше него.
                Maneuver next = ship.GetNextManeuver();
                if (next != null)
                {
                    into.Add(new Entry(ship.BurnStartEpoch, "M1 BURN", Kind.Burn, 0));
                    into.Add(new Entry(next.startEpoch, "M1 NODE", Kind.Node, 0));
                }
            }
            into.Sort((a, b) => a.Epoch.CompareTo(b.Epoch));
        }

        /// <summary>Ближайшая строка впереди, или null. Сработавший таймер уже позади.</summary>
        public static Entry? Next(Ship ship, Timers timers, double now, List<Entry> buffer)
        {
            Collect(ship, timers, now, buffer);
            foreach (Entry entry in buffer)
                if (entry.Epoch > now) return entry;
            return null;
        }

        static void AddTrajectory(TrajectoryCache trajectory, double now, List<Entry> into)
        {
            if (trajectory.patches != null)
            {
                foreach (TrajectoryPatch patch in trajectory.patches)
                {
                    if (patch.EndReason == PatchEndReason.Horizon || patch.EndEpoch <= now) continue;
                    into.Add(patch.EndReason == PatchEndReason.Impact
                        ? new Entry(patch.EndEpoch, "IMPACT", Kind.Impact)
                        : new Entry(patch.EndEpoch, $"SOI {patch.NextCentral.GameObject.name.ToUpperInvariant()}", Kind.Soi));
                }
            }
            if (trajectory.approaches == null) return;
            int shown = 0;
            foreach (CloseApproach approach in trajectory.approaches)
            {
                if (approach.Epoch <= now) continue;
                if (shown++ >= TrajectoryRenderer.ShownApproaches) break;
                into.Add(new Entry(approach.Epoch, $"APPR {approach.Distance / 1000.0:F1} km", Kind.Approach));
            }
        }
    }
}
