using System;
using UnityEngine;

namespace Interior
{
    /// <summary>Кнопка команды корабля. Ход и подпись задаются в AnimatedButtonControl.</summary>
    [ExecuteAlways]
    public class ButtonControl : AnimatedButtonControl
    {
        [Tooltip("Что делает эта кнопка. Ассет из папки разъёмов.")]
        public CommandPort port;

        [HideInInspector] public Action onPress;
        [HideInInspector] public Func<bool> available;

        public override string Prompt => port != null ? port.Title : label;

        public override bool Available => port != null
            ? ControlBus.Available(port.id)
            : onPress != null && (available == null || available());

        public override bool Wired => (port != null && port.Assigned) || onPress != null;

        protected override bool PerformAction()
        {
            if (port != null) ControlBus.Invoke(port.id);
            else onPress();
            return true;
        }
    }
}
