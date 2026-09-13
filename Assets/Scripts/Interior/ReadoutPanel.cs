using TMPro;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Малый экран панели: компонент не строит его, а пишет в него. Как экран выглядит —
    /// подложка, рамка, шрифт, кегль, размер — задаётся в сцене, потому что это внешний вид,
    /// а внешний вид правят глазами.
    ///
    /// Что нужно положить в поля: любой текстовый элемент TMP — хоть мировой TextMeshPro на
    /// меше, хоть TextMeshProUGUI на холсте. Компоненту всё равно, какой именно: он знает
    /// только про строки.
    /// </summary>
    public class ReadoutPanel : MonoBehaviour
    {
        [Tooltip("Куда писать строки. Если не заполнено — будет найден первый TMP среди детей.")]
        public TMP_Text text;

        /// <summary>
        /// Раз во сколько кадров пересчитывать строки. Показания меняются медленнее картинки,
        /// и собирать их каждый кадр незачем.
        /// </summary>
        public int everyFrames = 10;

        public string Text
        {
            get => text == null ? "" : text.text;
            set { if (text != null) text.text = value; }
        }

        public bool DueThisFrame => text != null && Time.frameCount % Mathf.Max(everyFrames, 1) == 0;

        void Awake()
        {
            if (text == null) text = GetComponentInChildren<TMP_Text>();
            if (text == null)
            {
                Debug.LogError($"ReadoutPanel на «{name}»: не указан текстовый элемент — экран останется пустым.", this);
            }
        }
    }
}
