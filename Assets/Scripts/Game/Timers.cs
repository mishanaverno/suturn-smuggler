using System.Collections.Generic;

namespace Game
{
    /// <summary>
    /// Таймеры бортовых часов. Таймер — момент срабатывания в эпохе, а не остаток: время
    /// в игре одно, и перемотка двигает всех разом. Паузы нет — ставить на паузу нечего.
    ///
    /// Секундомер — здесь же: момент запуска в эпохе, NaN — сброшен.
    ///
    /// Курсор живёт здесь, а не в экране или устройстве: экран его показывает, устройство
    /// двигает, и друг о друге им знать незачем.
    /// </summary>
    public class Timers
    {
        public sealed class Timer
        {
            public int Number;
            public double Epoch;
            public bool Fired;
        }

        /// <summary>Сатурнианские сутки, с: 10 ч 33 мин 38 с.</summary>
        public const double DayLength = 10 * 3600 + 33 * 60 + 38;

        readonly List<Timer> list = new();
        int nextNumber = 1;

        public IReadOnlyList<Timer> List => list;
        public int Cursor { get; private set; } = -1;
        public Timer Selected => Cursor >= 0 && Cursor < list.Count ? list[Cursor] : null;
        public bool AnyFired => list.Exists(t => t.Fired);
        public double StopwatchStart { get; private set; } = double.NaN;
        public bool StopwatchRunning => !double.IsNaN(StopwatchStart);

        public void StartStopwatch(double now) => StopwatchStart = now;

        public void ResetStopwatch() => StopwatchStart = double.NaN;

        public void Add(double epoch)
        {
            Timer timer = new() { Number = nextNumber++, Epoch = epoch };
            list.Add(timer);
            Sort(timer);
        }

        public void RemoveSelected()
        {
            if (Selected == null) return;
            list.RemoveAt(Cursor);
            if (list.Count == 0) nextNumber = 1;
            Cursor = list.Count == 0 ? -1 : Cursor % list.Count;
        }

        public void MoveCursor(int direction)
        {
            if (list.Count == 0) return;
            Cursor = (Cursor + (direction < 0 ? -1 : 1) + list.Count) % list.Count;
        }

        /// <summary>Сдвинуть выбранный таймер. Перенесённый в будущее срабатывает снова.</summary>
        public void ShiftSelected(double seconds, double now)
        {
            Timer timer = Selected;
            if (timer == null) return;
            timer.Epoch = System.Math.Max(timer.Epoch + seconds, now);
            timer.Fired = timer.Epoch <= now;
            Sort(timer);
        }

        /// <summary>Отметить наступившие. true — хоть один сработал именно сейчас.</summary>
        public bool Fire(double now)
        {
            bool fired = false;
            foreach (Timer timer in list)
            {
                if (timer.Fired || timer.Epoch > now) continue;
                timer.Fired = true;
                fired = true;
            }
            return fired;
        }

        /// <summary>Список держится по времени срабатывания, курсор остаётся на том же таймере.</summary>
        void Sort(Timer keep)
        {
            list.Sort((a, b) => a.Epoch.CompareTo(b.Epoch));
            Cursor = list.IndexOf(keep);
        }
    }
}
