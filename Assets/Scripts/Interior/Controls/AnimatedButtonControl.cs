using TMPro;
using UnityEngine;

namespace Interior
{
    /// <summary>Общий ход, подпись и отклики кнопок кабины.</summary>
    public abstract class AnimatedButtonControl : MonoBehaviour, IInteractable
    {
        const string CaptionName = "Label";

        [Tooltip("Утапливаемая деталь.")]
        public Transform part;

        [Tooltip("Куда утапливается, в местных осях детали. Нормализуется.")]
        public Vector3 axis = Vector3.down;

        [Tooltip("Глубина хода, метры.")]
        public float depth = 0.004f;

        public float downTime = 0.04f;
        public float upTime = 0.12f;

        [Tooltip("Что написано у кнопки.")]
        public string label = "";

        [Tooltip("Чем писать подпись. Пусто — кнопка без подписи.")]
        public GameObject labelPrefab;

        [Tooltip("Кегль подписи. 0 — как в префабе подписи.")]
        public float fontSize;

        [Tooltip("Где стоит подпись относительно детали, в её местных осях.")]
        public Vector3 labelOffset;

        [Tooltip("Как повёрнута подпись относительно детали, градусы.")]
        public Vector3 labelAngles;

        ControlResponse[] responses;
        Transform caption;
        Vector3 home;
        float phase = -1f;

        protected virtual void Awake()
        {
            responses = GetComponents<ControlResponse>();
            if (part != null) home = part.localPosition;
            ShowLabel();
        }

        protected virtual void OnDisable()
        {
            if (phase < 0f || part == null) return;
            phase = -1f;
            part.localPosition = home;
        }

        protected virtual void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                ShowLabel();
                return;
            }
#endif
            if (phase < 0f || part == null) return;

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

        /// <summary>Подпись создаётся из настроек кнопки и движется вместе с деталью.</summary>
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
            text.fontSize = fontSize > 0 ? fontSize : labelPrefab.GetComponentInChildren<TMP_Text>().fontSize;
        }

        public abstract string Prompt { get; }
        public abstract bool Available { get; }
        public abstract bool Wired { get; }

        public void Interact()
        {
            if (!Available || !PerformAction()) return;

            phase = 0f;
            foreach (ControlResponse response in responses) response.Play();
        }

        protected abstract bool PerformAction();
    }
}
