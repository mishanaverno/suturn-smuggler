using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Поворот галетного переключателя в положение. В отличие от KnobTurn, получает не знак
    /// щелчка, а номер положения: у переключателя есть место, где он должен стоять, и ручка
    /// идёт туда, откуда бы ни начинала.
    ///
    /// Углы задаются по одному на положение, потому что у настоящих галет шаг бывает
    /// неравным, и подбирать его удобнее глазами. Из последнего положения в первое ручка
    /// идёт обратно через все промежуточные углы, как галета с упором.
    /// </summary>
    public class SwitchTurn : ControlResponse
    {
        [Tooltip("Вокруг чего вращается, в местных осях объекта. Нормализуется.")]
        public Vector3 axis = Vector3.up;

        [Tooltip("Угол каждого положения от исходного поворота, градусы. Сколько углов — столько положений.")]
        public float[] angles = { 0f, 45f };

        [Tooltip("За сколько секунд ручка доходит до положения.")]
        public float stepTime = 0.09f;

        [Tooltip("Как идёт поворот: 0 — начало, 1 — конец. Выше единицы — перелёт с возвратом.")]
        public AnimationCurve motion = new(new Keyframe(0f, 0f, 0f, 3f), new Keyframe(1f, 1f, 0f, 0f));

        Quaternion home;
        float from;
        float target;
        float angle;
        float phase = -1f;

        void Awake()
        {
            home = transform.localRotation;
            OnDisable();
        }

        void OnDisable()
        {
            phase = -1f;
            from = target = angle = angles[0];
            Apply();
        }

        public override void Play() => Play(0);

        public override void Play(int position)
        {
            from = angle;
            target = angles[Mathf.Clamp(position, 0, angles.Length - 1)];
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

            Apply();
        }

        void Apply() => transform.localRotation = home * Quaternion.AngleAxis(angle, axis.normalized);
    }
}
