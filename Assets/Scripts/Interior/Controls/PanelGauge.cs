using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Стрелочный прибор: то же показание, что у табло, но стрелкой. Число читать надо, а
    /// стрелку видно краем глаза — «реактор ещё не вышел», «бак на исходе».
    ///
    /// Диапазон задаётся в тех же единицах, что пишет табло: к величине сначала применяется
    /// масштаб разъёма, поэтому для мощности реактора в процентах шкала — 0…100, а не 0…1.
    /// За пределами диапазона стрелка лежит на упоре. Нет показания — стрелка на нуле шкалы:
    /// обесточенный прибор так и выглядит.
    /// </summary>
    public class PanelGauge : MonoBehaviour
    {
        [Tooltip("Что показываем. Ассет из папки разъёмов.")]
        public ReadingPort port;

        [Tooltip("Стрелка. Пивот — в центре циферблата. Пусто — ищется дочерний «arrow».")]
        public Transform needle;

        [Tooltip("Ось вращения стрелки в её местных осях: нормаль циферблата.")]
        public Vector3 axis = Vector3.forward;

        [Tooltip("Начало и конец шкалы, в единицах табло (после масштаба разъёма).")]
        public float min;
        public float max = 100f;

        [Tooltip("Угол стрелки в начале и на конце шкалы, от положения в модели, °.")]
        public float minAngle;
        public float maxAngle = -270f;

        Quaternion rest;

        void Awake()
        {
            if (needle == null) needle = transform.Find("arrow");
            if (needle == null)
            {
                Debug.LogError($"PanelGauge на «{name}»: нет стрелки — положите её в поле needle.", this);
                enabled = false;
                return;
            }
            rest = needle.localRotation;
        }

        void Update()
        {
            float t = 0f;
            if (port != null && ControlBus.TryRead(port.id, out double value) && !double.IsNaN(value))
                t = Mathf.InverseLerp(min, max, (float)(value * port.scale));

            needle.localRotation = rest * Quaternion.AngleAxis(Mathf.Lerp(minAngle, maxAngle, t), axis);
        }
    }
}
