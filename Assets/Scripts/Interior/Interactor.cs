using TMPro;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Рука: луч, который дал IAim с того же объекта, и подпись того, на что он наведён.
    /// Компонент выключается вместе со своим занятием, поэтому двух рук одновременно
    /// не бывает.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class Interactor : MonoBehaviour
    {
        public Vector2 labelOffset = new(18f, -18f);

        IAim aim;
        TextMeshProUGUI label;
        IInteractable aimed;

        void Awake()
        {
            aim = GetComponentInParent<IAim>();
            label = CreateLabel();
        }

        void OnDisable()
        {
            if (label != null) label.text = "";
        }

        void Update()
        {
            if (aim == null) return;
            aimed = Aim();
            label.text = aimed == null ? "" : aimed.Prompt;
            label.rectTransform.anchoredPosition = aim.LabelPosition + labelOffset;
        }

        /// <summary>Нажатие приходит от занятия: у тела это клавиша, у пилота — кнопка мыши.</summary>
        public void Activate()
        {
            if (aimed != null) aimed.Interact();
        }

        /// <summary>Щелчок колеса по тому, на что наведён курсор.</summary>
        public void Scroll(int direction)
        {
            if (aimed is IScrollable knob) knob.Scroll(direction);
        }

        IInteractable Aim()
        {
            if (!Physics.Raycast(aim.Ray, out RaycastHit hit, aim.Reach, ~0, QueryTriggerInteraction.Collide))
            {
                return null;
            }
            IInteractable target = hit.collider.GetComponentInParent<IInteractable>();
            return target != null && target.Available ? target : null;
        }

        static TextMeshProUGUI CreateLabel()
        {
            GameObject canvasObject = new("AimLabel");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            GameObject textObject = new("Text");
            textObject.transform.SetParent(canvasObject.transform, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18f;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(420f, 40f);
            return text;
        }
    }
}
