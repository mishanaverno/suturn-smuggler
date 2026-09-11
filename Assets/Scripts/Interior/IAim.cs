using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Чем игрок показывает на орган управления. Это не одно и то же в двух занятиях:
    /// у летящего тела — направление взгляда, у пилота на месте — курсор, живущий отдельно
    /// от взгляда. Interactor знает только про луч и не знает, кто его дал.
    /// </summary>
    public interface IAim
    {
        Ray Ray { get; }
        /// <summary>Дальность руки, м.</summary>
        float Reach { get; }
        /// <summary>Где показать подпись, в пикселях окна.</summary>
        Vector2 LabelPosition { get; }
    }
}
