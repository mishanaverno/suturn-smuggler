using TMPro;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Рука: луч, который дал IAim с того же объекта, и подпись того, на что он наведён.
    /// Компонент выключается вместе со своим занятием, поэтому двух рук одновременно
    /// не бывает.
    ///
    /// Подпись компонент не создаёт: положите в поле готовый TMP на экранном холсте, и
    /// внешний вид подсказки — шрифт, цвет, размер — будет правиться в сцене, а не в коде.
    /// Без подписи рука работает молча: наводить и нажимать можно, читать нечего.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class Interactor : MonoBehaviour
    {
        [Tooltip("Подпись у курсора. Её RectTransform двигается за курсором, поэтому якоря должны быть в левом нижнем углу.")]
        public TextMeshProUGUI label;
        public Vector2 labelOffset = new(18f, -18f);

        IAim aim;
        IInteractable aimed;

        void Awake()
        {
            aim = GetComponentInParent<IAim>();
            if (aim == null)
            {
                Debug.LogError($"Interactor на «{name}»: рядом нет IAim — некому дать луч.", this);
            }
        }

        void OnDisable()
        {
            if (label != null) label.text = "";
        }

        void Update()
        {
            if (aim == null) return;
            aimed = Aim();
            if (label == null) return;
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

    }
}
