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
    /// не требует чувства времени — только счёта. По той же причине здесь нет разгона при
    /// быстром вращении: он вернул бы ту самую неопределённость, ради ухода от которой
    /// крутилку и завели.
    ///
    /// Величины крутилка не знает: она отдаёт знак, а насколько сдвинуть — решает устройство.
    /// Что получилось, человек читает на табло рядом (PanelReading), а не по положению ручки.
    /// </summary>
    public class PanelKnob : MonoBehaviour, IInteractable, IScrollable
    {
        [Tooltip("Что эта крутилка крутит. Ассет из папки разъёмов.")]
        public StepPort port;

        [Tooltip("Подпись для старой проводки по имени. При заполненном разъёме не используется.")]
        public string label = "";

        [HideInInspector] public Action<int> onStep;
        [HideInInspector] public Func<bool> available;

        ControlResponse[] responses;

        void Awake() => responses = GetComponents<ControlResponse>();

        public string Prompt => port != null ? port.Title : label;

        public bool Available => port != null
            ? ControlBus.Available(port.id)
            : onStep != null && (available == null || available());

        public bool Wired => (port != null && port.Assigned) || onStep != null;

        /// <summary>Крутилку не нажимают: щелчок по ней ничего не значит.</summary>
        public void Interact() { }

        public void Scroll(int direction)
        {
            if (!Available || direction == 0) return;

            if (port != null) ControlBus.Turn(port.id, direction);
            else onStep(direction);

            foreach (ControlResponse response in responses) response.Play(direction);
        }
    }
}
