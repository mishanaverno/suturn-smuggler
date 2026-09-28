using DoublePrecision;

namespace Game
{
    /// <summary>
    /// Одно правило вместо прежней лестницы: за GuardSeconds до любого события перемотки нет.
    /// События — строки расписания (Schedule), то есть всё, где игрок должен смотреть, а не ждать.
    ///
    /// Зажим шага нужен не как вторая автоматика, а чтобы правило вообще выполнялось: на
    /// 100000× один кадр продвигает время на четверть часа, и окно в тридцать секунд было бы
    /// проскочено целиком, не успев сработать. Поэтому перед событием перемотка ограничена
    /// так, чтобы кадр не перепрыгнул границу окна.
    /// </summary>
    public static class WarpLimit
    {
        public const double GuardSeconds = 30.0;

        public static double Allowed(double requested, double timeToEvent, double frameSeconds)
        {
            if (requested <= 1.0) return requested;
            if (timeToEvent <= GuardSeconds) return 1.0;
            if (frameSeconds <= 0.0) return requested;
            double untilGuard = (timeToEvent - GuardSeconds) / frameSeconds;
            return Mathd.Clamp(untilGuard, 1.0, requested);
        }
    }
}
