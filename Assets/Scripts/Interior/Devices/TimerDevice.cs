using Game;

namespace Interior
{
    /// <summary>
    /// Задатчик таймеров бортовых часов: создать, выбрать, выставить крутилкой, снять.
    /// И секундомер: пуск и сброс.
    /// Сами часы и список — на экране CLOCK (ClockPanel); задатчик от экрана не зависит.
    ///
    /// Новый таймер ставится на час вперёд и выставляется крутилкой: вводить число в кабине
    /// нечем, а таймер на «сейчас» сработал бы, не дав себя выставить. Щелчок крутилки —
    /// час, минута или секунда, смотря какая из трёх кнопок выбрана.
    /// </summary>
    public class TimerDevice : ShipDevice
    {
        const double Hour = 3600.0;
        const double Minute = 60.0;
        const double Second = 1.0;

        double unit = Hour;

        protected override void Wire()
        {
            Bind(CommandId.NewTimer, () => Timers.Add(Now + Hour), () => Timers != null);
            Bind(CommandId.DeleteTimer, () => Timers.RemoveSelected(), HasSelected);
            Bind(CommandId.NextTimer, () => Timers.MoveCursor(1), HasSelected);
            Bind(CommandId.PreviousTimer, () => Timers.MoveCursor(-1), HasSelected);

            Bind(StepId.TimerTime,
                direction => Timers.ShiftSelected(direction * unit, Now), HasSelected);

            Bind(CommandId.TimerUnitHours, () => unit = Hour);
            Bind(CommandId.TimerUnitMinutes, () => unit = Minute);
            Bind(CommandId.TimerUnitSeconds, () => unit = Second);

            Bind(CommandId.StopwatchStart, () => Timers.StartStopwatch(Now),
                () => Timers != null && !Timers.StopwatchRunning);
            Bind(CommandId.StopwatchReset, () => Timers.ResetStopwatch(),
                () => Timers != null && Timers.StopwatchRunning);

            Bind(SignalId.TimerFired, () => Timers != null && Timers.AnyFired);
            Bind(SignalId.StopwatchRunning, () => Timers != null && Timers.StopwatchRunning);
            Bind(SignalId.TimerUnitHours, () => unit == Hour);
            Bind(SignalId.TimerUnitMinutes, () => unit == Minute);
            Bind(SignalId.TimerUnitSeconds, () => unit == Second);

            Bind(ReadingId.SelectedTimer, () => HasSelected() ? Timers.Selected.Epoch - Now : double.NaN);
            Bind(ReadingId.Stopwatch, () => Timers == null ? double.NaN : Now - Timers.StopwatchStart);
        }

        static Timers Timers => GameMono.instance?.timers;

        static double Now => GameMono.instance.Epoch;

        static bool HasSelected() => Timers?.Selected != null;
    }
}
