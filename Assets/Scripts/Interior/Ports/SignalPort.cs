using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Показание: корабль отвечает «да» или «нет». Сюда садится лампа.
    ///
    /// Только чтение. Показание — не то же самое, что команда, которая его меняет:
    /// «нажали кнопку двигателя» и «двигатель работает» расходятся на время выхода на режим,
    /// при автоотсечке и при отказе. Разведены разъёмами, чтобы могли разойтись и на панели.
    /// </summary>
    [CreateAssetMenu(menuName = "Cockpit/Port/Signal", fileName = "Signal Port")]
    public sealed class SignalPort : ControlPort
    {
        [Tooltip("Какое показание. Проставляется генератором разъёмов, руками трогать незачем.")]
        public SignalId id = SignalId.None;

        public override bool Assigned => id != SignalId.None;
    }
}
