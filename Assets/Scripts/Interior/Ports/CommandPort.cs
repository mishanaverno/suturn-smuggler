using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Команда: нажали — произошло. Сюда садится кнопка.
    ///
    /// Переключатель отдельным видом разъёма не заводится: «включить/выключить двигатель» —
    /// это одна команда, которая сама смотрит на текущее состояние. Кнопке знать об этом
    /// незачем, а лампа рядом узнаёт результат своим разъёмом-показанием, а не фактом нажатия.
    /// </summary>
    [CreateAssetMenu(menuName = "Cockpit/Port/Command", fileName = "Command Port")]
    public sealed class CommandPort : ControlPort
    {
        [Tooltip("Какое дело кокпита. Проставляется генератором разъёмов, руками трогать незачем.")]
        public CommandId id = CommandId.None;

        public override bool Assigned => id != CommandId.None;
    }
}
