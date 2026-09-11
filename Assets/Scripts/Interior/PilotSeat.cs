using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Вход на место пилота — люк позади кресла. Пилот проходит в люк и сразу оказывается
    /// пристёгнутым: между «зашёл» и «на месте» ничего не происходит, потому что в тесной
    /// кабине идти некуда.
    /// </summary>
    public class PilotSeat : MonoBehaviour, IInteractable
    {
        public PilotMono pilot;

        public string Prompt => "E — take the pilot seat";
        public bool Available => pilot != null && !pilot.Active;

        public void Interact() => pilot.TakeOver();
    }
}
