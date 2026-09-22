using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Величина: орган выставляет её целиком. Сюда садится рычаг.
    ///
    /// Оформления, в отличие от показания, здесь нет: множитель и единицу читает человек,
    /// а устройство принимает долю хода и переводит её само.
    /// </summary>
    [CreateAssetMenu(menuName = "Cockpit/Port/Setting", fileName = "Setting Port")]
    public sealed class SettingPort : ControlPort
    {
        [Tooltip("Какую величину выставляет. Проставляется генератором разъёмов, руками трогать незачем.")]
        public SettingId id = SettingId.None;

        public override bool Assigned => id != SettingId.None;
    }
}
