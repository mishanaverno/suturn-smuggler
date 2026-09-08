using DoublePrecision;

namespace Game
{
    /// <summary>
    /// Предел перемотки по ближайшему событию. Лестница ступеней задаёт только желаемую скорость,
    /// а фактическая получается делением: до события всегда остаётся не меньше LeadSeconds
    /// реальных секунд, поэтому перемотка не падает ступенькой, а плавно съезжает.
    /// </summary>
    public static class WarpLimit
    {
        // За столько реальных секунд до события перемотка опускается до 1×.
        public const double LeadSeconds = 5.0;
        // Доля окна пролёта сферы влияния, которую разрешено проходить за один кадр.
        public const double WindowFraction = 0.25;

        public static double Allowed(double requested, double timeToEvent) =>
            requested <= 1.0 ? requested : Mathd.Clamp(timeToEvent / LeadSeconds, 1.0, requested);

        /// <summary>
        /// Страховка сверх основного механизма: прогноз пересчитывается раз в несколько кадров
        /// и может устареть. Дешёвая проверка на случай, когда предсказание не сработало.
        /// </summary>
        public static double FrameCap(double flybyWindow, double frameSeconds) =>
            Mathd.Max(flybyWindow * WindowFraction / frameSeconds, 1.0);
    }
}
