using UnityEngine;
using UnityEngine.Serialization;

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
    ///
    /// Компонент живёт на корне органа, стрелка — в поле part.
    /// </summary>
    public class GaugeIndicator : MonoBehaviour
    {
        [Tooltip("Что показываем. Ассет из папки разъёмов.")]
        public ReadingPort port;

        [Tooltip("Стрелка. Пивот — в центре циферблата.")]
        [FormerlySerializedAs("needle")]
        public Transform part;

        [Tooltip("Ось вращения стрелки в её местных осях: нормаль циферблата.")]
        public Vector3 axis = Vector3.forward;

        [Tooltip("Начало и конец шкалы, в единицах табло (после масштаба разъёма).")]
        public float min;
        public float max = 100f;

        [Tooltip("Угол стрелки в начале и на конце шкалы, от положения в модели, °.")]
        public float minAngle = -45f;
        public float maxAngle = 225f;

        Quaternion rest;

        void Awake() => rest = part.localRotation;

        void Update()
        {
            float t = 0f;
            if (port != null && ControlBus.TryRead(port.id, out double value) && !double.IsNaN(value))
                t = Mathf.InverseLerp(min, max, (float)(value * port.scale));

            part.localRotation = rest * Quaternion.AngleAxis(Mathf.Lerp(minAngle, maxAngle, t), axis);
        }
    }
}
