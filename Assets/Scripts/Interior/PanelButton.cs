using System;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Кнопка на панели. Что она делает, знает не она, а CockpitControls: панель — это железо,
    /// а смысл железа задаётся проводкой. Так один и тот же кубик служит и «новым манёвром»,
    /// и чем угодно ещё, а перечень действий читается в одном месте.
    /// </summary>
    public class PanelButton : MonoBehaviour, IInteractable
    {
        public string label = "";
        public Action onPress;
        public Func<bool> available;

        public string Prompt => label;
        public bool Available => onPress != null && (available == null || available());

        public void Interact() => onPress?.Invoke();
    }
}
