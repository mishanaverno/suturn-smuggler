using System;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Галетный переключатель: ручка с несколькими положениями, каждое — своя команда.
    /// Щелчок колеса переводит на соседнее положение по кругу и выполняет его команду.
    ///
    /// Колесом, а не нажатием, потому что у ручки есть направление: крутят её в обе стороны,
    /// и проскочившее положение возвращают тем же движением руки, а не полным кругом.
    ///
    /// Разгона, в отличие от крутилки, нет: положений мало, и щелчок колеса всегда значит
    /// ровно одно положение.
    ///
    /// Если команда соседнего положения сейчас недоступна, ручка остаётся на месте:
    /// переключатель, вставший на положение, которое ничего не сделало, врал бы глазам.
    /// </summary>
    public class PanelSwitcher : MonoBehaviour, IInteractable, IScrollable
    {
        const int MaxPositions = 6;

        [Tooltip("Команды положений по порядку, не больше шести. Первое — положение при запуске.")]
        public CommandPort[] ports = Array.Empty<CommandPort>();

        ControlResponse[] responses;
        int position;

        void Awake() => responses = GetComponents<ControlResponse>();

        void OnValidate()
        {
            if (ports.Length > MaxPositions) Array.Resize(ref ports, MaxPositions);
        }

        /// <summary>Подпись — положение, в котором ручка стоит: куда её повернут, решает рука.</summary>
        public string Prompt => ports.Length == 0 || ports[position] == null ? name : ports[position].Title;

        /// <summary>Доступна, пока есть куда повернуть хотя бы в одну сторону.</summary>
        public bool Available => Ready(1) || Ready(-1);

        public bool Wired => Array.Exists(ports, port => port != null && port.Assigned);

        /// <summary>Галету не нажимают: щелчок по ней ничего не значит, её крутят.</summary>
        public void Interact() { }

        public void Scroll(int direction)
        {
            if (direction == 0) return;
            int sign = direction > 0 ? 1 : -1;
            if (!Ready(sign)) return;

            position = Step(sign);
            ControlBus.Invoke(ports[position].id);

            foreach (ControlResponse response in responses) response.Play(position);
        }

        bool Ready(int sign)
        {
            if (ports.Length == 0) return false;
            CommandPort next = ports[Step(sign)];
            return next != null && ControlBus.Available(next.id);
        }

        int Step(int sign) => (position + sign + ports.Length) % ports.Length;
    }
}
