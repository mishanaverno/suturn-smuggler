using System;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Кнопка на панели. Что она делает, знает не она, а разъём в её поле: ссылка на ассет,
    /// который проводка (CockpitControls) связала с делом корабля. Один и тот же кубик служит
    /// чем угодно, а перевесить его — значит поменять ссылку, не трогая код.
    ///
    /// Собственное поведение кнопки — ход, звук, подсветка — живёт в компонентах
    /// ControlResponse на этом же объекте. Кнопка их находит и дёргает, но только когда
    /// команда прошла: нажатая вхолостую кнопка не должна выглядеть нажатой.
    ///
    /// Поля onPress/available — старая проводка по имени объекта. Работают, пока не переехали
    /// все органы; разъём, если он есть, всегда главнее.
    /// </summary>
    public class PanelButton : MonoBehaviour, IInteractable
    {
        [Tooltip("Что делает эта кнопка. Ассет из папки разъёмов; перечень дел — в CockpitControls.")]
        public CommandPort port;

        [Tooltip("Подпись для старой проводки по имени. При заполненном разъёме не используется.")]
        public string label = "";

        [HideInInspector] public Action onPress;
        [HideInInspector] public Func<bool> available;

        ControlResponse[] responses;

        void Awake() => responses = GetComponents<ControlResponse>();

        public string Prompt => port != null ? port.Title : label;

        public bool Available => port != null
            ? ControlBus.Available(port.id)
            : onPress != null && (available == null || available());

        /// <summary>Подключена ли вообще: проверяет проводка, чтобы немая кнопка не молчала.</summary>
        public bool Wired => (port != null && port.Assigned) || onPress != null;

        public void Interact()
        {
            if (!Available) return;

            if (port != null) ControlBus.Invoke(port.id);
            else onPress();

            foreach (ControlResponse response in responses) response.Play();
        }
    }
}
