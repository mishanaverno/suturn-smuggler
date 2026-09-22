using System;
using TMPro;
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
    [ExecuteAlways]
    public class PanelButton : MonoBehaviour, IInteractable
    {
        const string CaptionName = "Label";

        [Tooltip("Что делает эта кнопка. Ассет из папки разъёмов; перечень дел — в CockpitControls.")]
        public CommandPort port;

        [Tooltip("Что написано у кнопки. Заодно подпись для старой проводки по имени.")]
        public string label = "";

        [Tooltip("Чем писать подпись. Пусто — кнопка без подписи.")]
        public GameObject labelPrefab;

        [Tooltip("Кегль подписи. 0 — как в префабе подписи.")]
        public float fontSize;

        [Tooltip("Где стоит подпись относительно кнопки, в её местных осях.")]
        public Vector3 labelOffset;

        [Tooltip("Как повёрнута подпись относительно кнопки, градусы.")]
        public Vector3 labelAngles;

        [HideInInspector] public Action onPress;
        [HideInInspector] public Func<bool> available;

        ControlResponse[] responses;
        Transform caption;

        void Awake()
        {
            responses = GetComponents<ControlResponse>();
            ShowLabel();
        }

#if UNITY_EDITOR
        void Update()
        {
            if (!Application.isPlaying) ShowLabel();
        }
#endif

        /// <summary>
        /// Подпись — не часть сцены, а следствие полей кнопки: объект не сохраняется, а
        /// собирается заново и в редакторе, и на старте. Поэтому её нельзя сдвинуть мышью
        /// мимо соседей, одинаковые кнопки подписаны одинаково, а в файле сцены от подписи
        /// не остаётся ни строчки. Отступ и поворот задаются на префабе кнопки: в сцене
        /// правится только текст.
        /// </summary>
        void ShowLabel()
        {
            if (caption == null) caption = transform.Find(CaptionName);

            if (labelPrefab == null)
            {
                if (caption != null) DestroyImmediate(caption.gameObject);
                caption = null;
                return;
            }

            if (caption == null)
            {
                caption = Instantiate(labelPrefab, transform).transform;
                caption.name = CaptionName;
                caption.gameObject.hideFlags = HideFlags.DontSave;
            }

            caption.localPosition = labelOffset;
            caption.localEulerAngles = labelAngles;
            TMP_Text text = caption.GetComponentInChildren<TMP_Text>();
            text.text = label;
            // Берём кегль из префаба, а не оставляем текущий: иначе сброс поля в 0 в редакторе не вернёт исходный размер.
            text.fontSize = fontSize > 0 ? fontSize : labelPrefab.GetComponentInChildren<TMP_Text>().fontSize;
        }

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
