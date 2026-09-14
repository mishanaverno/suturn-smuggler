using TMPro;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Табло: одно число рядом с органом. Компонент не строит его, а пишет в него — как оно
    /// выглядит, задаётся в сцене, потому что это внешний вид.
    ///
    /// Крутилка вслепую бесполезна: подкрутил Δv, а на сколько — не видно. Табло и есть ответ.
    /// Оно живёт отдельным компонентом со своим разъёмом, поэтому его можно поставить и рядом
    /// с органом, и на другой конец панели, и вовсе без органа — как показание прибора.
    ///
    /// Устройство отдаёт величину в системных единицах, оформление берётся из разъёма.
    /// Если устройства нет или оно неисправно, табло гаснет: пустая строка честнее нуля.
    /// </summary>
    public class PanelReading : MonoBehaviour
    {
        [Tooltip("Что показываем. Ассет из папки разъёмов.")]
        public ReadingPort port;

        [Tooltip("Куда писать: любой текст TMP — мировой на меше или на холсте.")]
        public TMP_Text text;

        [Tooltip("Раз во сколько кадров опрашивать. Показания меняются медленнее картинки.")]
        public int everyFrames = 5;

        [Tooltip("Что писать, когда показания нет.")]
        public string blank = "—";

        void Awake()
        {
            if (text == null) text = GetComponentInChildren<TMP_Text>();
            if (text == null)
            {
                Debug.LogError($"PanelReading на «{name}»: некуда писать — положите в поле текст TMP.", this);
                enabled = false;
            }
        }

        void Update()
        {
            if (port == null || Time.frameCount % Mathf.Max(everyFrames, 1) != 0) return;

            text.text = ControlBus.TryRead(port.id, out double value) && !double.IsNaN(value)
                ? port.Format(value)
                : blank;
        }
    }
}
