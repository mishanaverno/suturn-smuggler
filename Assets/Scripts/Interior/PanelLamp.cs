using System;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Лампа. Не орган управления, а ответ прибора: горит или не горит. Условие задаёт
    /// проводка, лампа знает только два цвета.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class PanelLamp : MonoBehaviour
    {
        public Color on = new(1f, 0.35f, 0.3f);
        public Color off = new(0.18f, 0.08f, 0.08f);
        public Func<bool> lit;
        public int everyFrames = 5;

        Renderer surface;
        bool state;

        void Awake()
        {
            surface = GetComponent<Renderer>();
            Apply(false);
        }

        void Update()
        {
            if (lit == null || Time.frameCount % Mathf.Max(everyFrames, 1) != 0) return;
            bool next = lit();
            if (next != state) Apply(next);
        }

        void Apply(bool value)
        {
            state = value;
            surface.material.color = value ? on : off;
        }
    }
}
