using System;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Орган, который крутят колесом мыши, наведя на него курсор. Отдельным интерфейсом,
    /// потому что вращение — не «много нажатий»: у него есть знак и нет состояния «держат».
    /// </summary>
    public interface IScrollable
    {
        /// <summary>Щелчок колеса: +1 или −1.</summary>
        void Scroll(int direction);
    }

    /// <summary>
    /// Крутилка. Колесо под курсором — один щелчок, один шаг: величина меняется ровно
    /// настолько, насколько повернули, и не убегает, пока рука лежит на органе.
    ///
    /// Тем и лучше рычага с удержанием, который тут был раньше: у рычага величина ползёт,
    /// пока держишь, и чтобы попасть в нужное значение, надо отпустить вовремя. Крутилка
    /// не требует чувства времени — только счёта.
    ///
    /// Что она крутит, знает не она, а CockpitControls: панель — это железо, смысл железа
    /// задаётся проводкой.
    /// </summary>
    public class PanelKnob : MonoBehaviour, IInteractable, IScrollable
    {
        public string label = "";
        public Action<int> onStep;
        public Func<bool> available;

        public string Prompt => label;
        public bool Available => onStep != null && (available == null || available());

        /// <summary>Крутилку не нажимают: щелчок по ней ничего не значит.</summary>
        public void Interact() { }

        public void Scroll(int direction) => onStep?.Invoke(direction);
    }
}
