using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Interior
{
    /// <summary>
    /// Маленький экран на панели: тёмная подложка и строки текста, в мире, на своём месте.
    /// Не RenderTexture: рисовать текст камерой в текстуру, чтобы потом показать её на
    /// плоскости, — лишний круг. Камера нужна навигационному экрану, потому что там сцена;
    /// здесь только буквы.
    ///
    /// Размер задаётся в метрах, а плотность — пикселями на метр: так «кегль 16» означает
    /// определённую высоту буквы на стекле, а не случайную долю экрана.
    /// </summary>
    public class ReadoutPanel : MonoBehaviour
    {
        public float widthMeters = 0.22f;
        public float heightMeters = 0.16f;
        public float pixelsPerMeter = 1400f;
        public float fontSize = 20f;
        public Color background = new(0.02f, 0.04f, 0.03f, 1f);
        public Color ink = new(0.55f, 1f, 0.7f, 1f);
        /// <summary>Пересчитывать не каждый кадр: строки меняются медленнее картинки.</summary>
        public int everyFrames = 10;

        TextMeshProUGUI text;

        public string Text
        {
            get => text == null ? "" : text.text;
            set { if (text != null) text.text = value; }
        }

        public bool DueThisFrame => Time.frameCount % Mathf.Max(everyFrames, 1) == 0;

        void Awake()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(widthMeters * pixelsPerMeter, heightMeters * pixelsPerMeter);
            rect.localScale = Vector3.one / pixelsPerMeter;

            Image plate = gameObject.AddComponent<Image>();
            plate.color = background;
            plate.raycastTarget = false;

            GameObject textObject = new("Text");
            textObject.transform.SetParent(transform, false);
            text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = ink;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.TopLeft;

            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            float pad = 0.01f * pixelsPerMeter;
            textRect.offsetMin = new Vector2(pad, pad);
            textRect.offsetMax = new Vector2(-pad, -pad);
        }
    }
}
