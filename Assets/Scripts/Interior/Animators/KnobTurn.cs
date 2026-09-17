using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Поворот ручки на щелчок. Ось — в местных координатах объекта, поэтому крутилка,
    /// поставленная на панель под любым углом, вращается вокруг своей ножки.
    ///
    /// Ручка считает щелчки, а не показывает величину: «правильного» положения у неё нет,
    /// и догонять его она не пытается. Сколько получилось — читается на табло рядом.
    ///
    /// Движение задаётся кривой, а не одной скоростью: щелчок настоящей ручки — это рывок
    /// с доводкой, и разница между рывком и равномерным доворотом видна глазом. Кривую правят
    /// в инспекторе, потому что это внешний вид. Пружинку с перелётом рисуют тем же способом —
    /// кривой, уходящей выше единицы.
    /// </summary>
    public class KnobTurn : ControlResponse
    {
        [Tooltip("Вокруг чего вращается, в местных осях объекта. Нормализуется.")]
        public Vector3 axis = Vector3.up;

        [Tooltip("Градусов за один щелчок.")]
        public float degreesPerStep = 24f;

        [Tooltip("За сколько секунд отрабатывается один щелчок.")]
        public float stepTime = 0.09f;

        [Tooltip("Как идёт поворот внутри щелчка: 0 — начало, 1 — конец. Выше единицы — перелёт с возвратом.")]
        public AnimationCurve motion = new(new Keyframe(0f, 0f, 0f, 3f), new Keyframe(1f, 1f, 0f, 0f));

        [Header("Упоры")]
        [Tooltip("Ограничить поворот. Ручка с упорами не крутится дальше края, даже если щелчки идут.")]
        public bool limited;

        public float minAngle = -120f;
        public float maxAngle = 120f;

        Quaternion home;
        float from;
        float target;
        float angle;
        float phase = -1f;

        void Awake() => home = transform.localRotation;

        void OnDisable()
        {
            phase = -1f;
            from = target = angle = 0f;
            transform.localRotation = home;
        }

        public override void Play() => Play(1);

        /// <summary>
        /// Каждый щелчок начинается с того места, где ручка сейчас, а не с прошлой цели:
        /// иначе быстрая серия щелчков рвала бы движение.
        /// </summary>
        public override void Play(int direction)
        {
            from = angle;
            target += degreesPerStep * Mathf.Sign(direction);
            if (limited) target = Mathf.Clamp(target, minAngle, maxAngle);
            phase = 0f;
        }

        void Update()
        {
            if (phase < 0f) return;

            float span = Mathf.Max(stepTime, 0.0001f);
            phase += Time.deltaTime;

            if (phase >= span)
            {
                phase = -1f;
                angle = target;
            }
            else
            {
                angle = Mathf.LerpUnclamped(from, target, motion.Evaluate(phase / span));
            }

            transform.localRotation = home * Quaternion.AngleAxis(angle, axis.normalized);
        }
    }
}
