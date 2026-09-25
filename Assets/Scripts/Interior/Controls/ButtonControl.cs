using System;
using TMPro;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Кнопка на панели. Что она делает, знает не она, а разъём в её поле: ссылка на ассет,
    /// который проводка связала с делом корабля. Один и тот же кубик служит чем угодно, а
    /// перевесить его — значит поменять ссылку, не трогая код.
    ///
    /// Компонент живёт на корне органа, утапливаемая деталь — в поле part. Кнопка ходит,
    /// только когда команда прошла: нажатая вхолостую кнопка не должна выглядеть нажатой.
    /// Обратно она идёт медленнее, чем вниз: нажатие резкое, возврат пружинный.
    ///
    /// Поля onPress/available — старая проводка по имени объекта. Работают, пока не переехали
    /// все органы; разъём, если он есть, всегда главнее.
    /// </summary>
    [ExecuteAlways]
    public class ButtonControl : MonoBehaviour, IInteractable
    {
        const string CaptionName = "Label";

        [Tooltip("Что делает эта кнопка. Ассет из папки разъёмов.")]
        public CommandPort port;

        [Tooltip("Утапливаемая деталь.")]
        public Transform part;

        [Tooltip("Куда утапливается, в местных осях детали. Нормализуется.")]
        public Vector3 axis = Vector3.down;

        [Tooltip("Глубина хода, метры.")]
        public float depth = 0.004f;

        public float downTime = 0.04f;
        public float upTime = 0.12f;

        [Tooltip("Что написано у кнопки. Заодно подпись для старой проводки по имени.")]
        public string label = "";

        [Tooltip("Чем писать подпись. Пусто — кнопка без подписи.")]
        public GameObject labelPrefab;

        [Tooltip("Кегль подписи. 0 — как в префабе подписи.")]
        public float fontSize;

        [Tooltip("Где стоит подпись относительно детали, в её местных осях.")]
        public Vector3 labelOffset;

        [Tooltip("Как повёрнута подпись относительно детали, градусы.")]
        public Vector3 labelAngles;

        [HideInInspector] public Action onPress;
        [HideInInspector] public Func<bool> available;

        ControlResponse[] responses;
        Transform caption;
        Vector3 home;
        float phase = -1f;

        void Awake()
        {
            responses = GetComponents<ControlResponse>();
            if (part != null) home = part.localPosition;
            ShowLabel();
        }

        void OnDisable()
        {
            if (phase < 0f || part == null) return;
            phase = -1f;
            part.localPosition = home;
        }

        void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                ShowLabel();
                return;
            }
#endif
            if (phase < 0f) return;

            phase += Time.deltaTime;
            float down = Mathf.Max(downTime, 0.0001f);
            float up = Mathf.Max(upTime, 0.0001f);
            if (phase >= down + up)
            {
                phase = -1f;
                part.localPosition = home;
                return;
            }
            float amount = phase < down ? phase / down : 1f - (phase - down) / up;
            part.localPosition = home + axis.normalized * (depth * amount);
        }

        /// <summary>
        /// Подпись — не часть сцены, а следствие полей кнопки: объект не сохраняется, а
        /// собирается заново и в редакторе, и на старте. Поэтому её нельзя сдвинуть мышью
        /// мимо соседей, одинаковые кнопки подписаны одинаково, а в файле сцены от подписи
        /// не остаётся ни строчки. Висит она на детали и ходит вместе с ней.
        /// </summary>
        void ShowLabel()
        {
            if (part == null) return;
            if (caption == null) caption = part.Find(CaptionName);

            if (labelPrefab == null)
            {
                if (caption != null) DestroyImmediate(caption.gameObject);
                caption = null;
                return;
            }

            if (caption == null)
            {
                caption = Instantiate(labelPrefab, part).transform;
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

            phase = 0f;
            foreach (ControlResponse response in responses) response.Play();
        }
    }
}
