using UnityEngine;

namespace Interior
{
    /// <summary>Как показать число человеку.</summary>
    public enum ReadingFormat
    {
        /// <summary>Обычное число с фиксированным числом знаков после запятой.</summary>
        Number = 0,

        /// <summary>Секунды как ч:мм:сс — для времени до узла и длительности прожига.</summary>
        Duration = 1,
    }

    /// <summary>
    /// Числовое показание: одно число, которое устройство отдаёт, а табло показывает.
    ///
    /// Единица, множитель и знаки после запятой лежат здесь, а не в устройстве, потому что
    /// это оформление: корабль считает в метрах, а человеку у крутилки дальности нужны
    /// километры. Устройство отдаёт величину в системных единицах и ничего не знает о том,
    /// как её напишут.
    /// </summary>
    [CreateAssetMenu(menuName = "Cockpit/Port/Reading", fileName = "Reading Port")]
    public sealed class ReadingPort : ControlPort
    {
        [Tooltip("Какое показание. Проставляется генератором разъёмов, руками трогать незачем.")]
        public ReadingId id = ReadingId.None;

        [Tooltip("На что умножить системную величину перед показом: 0.001 переводит метры в километры.")]
        public double scale = 1.0;

        [Tooltip("Приписка после числа: «км», «м/с». Пусто — без приписки.")]
        public string unit = "";

        [Tooltip("Знаков после запятой. Для Duration не используется.")]
        public int digits = 0;

        public ReadingFormat format = ReadingFormat.Number;

        public override bool Assigned => id != ReadingId.None;

        /// <summary>Собрать строку из величины в системных единицах.</summary>
        public string Format(double value)
        {
            if (format == ReadingFormat.Duration) return Duration(value);
            string number = (value * scale).ToString("F" + Mathf.Clamp(digits, 0, 6));
            return string.IsNullOrEmpty(unit) ? number : $"{number} {unit}";
        }

        static string Duration(double seconds)
        {
            string sign = seconds < 0 ? "−" : "";
            long total = (long)System.Math.Abs(seconds);
            long hours = total / 3600;
            long minutes = total % 3600 / 60;
            long rest = total % 60;
            return $"{sign}{hours}:{minutes:00}:{rest:00}";
        }
    }
}
