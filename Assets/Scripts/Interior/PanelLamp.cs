using System;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Лампа. Не орган управления, а ответ прибора: горит или не горит. Что именно она
    /// показывает, задаёт разъём-показание в её поле.
    ///
    /// Лампа рядом с кнопкой — это не часть кнопки, а отдельный компонент со своим разъёмом.
    /// Поэтому «кнопка с индикатором» не требует нового класса: два компонента на одном
    /// объекте, две ссылки. И поэтому же индикатор может показывать не факт нажатия, а факт
    /// работы — они расходятся при выходе на режим и при автоотсечке.
    ///
    /// Поле lit — старая проводка по имени объекта. Разъём, если он есть, главнее.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class PanelLamp : MonoBehaviour
    {
        [Tooltip("Что показывает эта лампа. Ассет из папки разъёмов.")]
        public SignalPort port;

        public Color on = new(1f, 0.35f, 0.3f);
        public Color off = new(0.18f, 0.08f, 0.08f);

        [Tooltip("Раз во сколько кадров опрашивать. Лампа не обязана успевать за кадром.")]
        public int everyFrames = 5;

        [HideInInspector] public Func<bool> lit;

        Renderer surface;
        bool state;

        public bool Wired => (port != null && port.Assigned) || lit != null;

        void Awake()
        {
            surface = GetComponent<Renderer>();
            Apply(false);
        }

        void Update()
        {
            if (!Wired || Time.frameCount % Mathf.Max(everyFrames, 1) != 0) return;
            bool next = port != null ? ControlBus.Read(port.id) : lit();
            if (next == state) return;
            state = next;
            Apply(next);
        }

        /// <summary>
        /// Как лампа показывает состояние. Виртуально: другой индикатор — свечение, стрелка,
        /// анимация — это наследник, а не второй разъём и не правка проводки. Учёт состояния
        /// ведёт база, наследнику остаётся только показать.
        /// </summary>
        protected virtual void Apply(bool value) => surface.material.color = value ? on : off;
    }
}
