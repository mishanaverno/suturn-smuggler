using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Ход кнопки: утопить и отпустить. Ось и глубина — в местных координатах объекта,
    /// поэтому повёрнутая на панели кнопка ходит правильно, а подобрать ход можно глазами.
    ///
    /// Обратно кнопка идёт медленнее, чем вниз: нажатие резкое, возврат пружинный.
    /// </summary>
    public class ButtonTravel : ControlResponse
    {
        [Tooltip("Куда утапливается, в местных осях объекта. Нормализуется.")]
        public Vector3 axis = Vector3.down;

        [Tooltip("Глубина хода, метры.")]
        public float depth = 0.004f;

        public float downTime = 0.04f;
        public float upTime = 0.12f;

        Vector3 home;
        float phase = -1f;

        void Awake() => home = transform.localPosition;

        void OnDisable()
        {
            phase = -1f;
            transform.localPosition = home;
        }

        public override void Play() => phase = 0f;

        void Update()
        {
            if (phase < 0f) return;

            phase += Time.deltaTime;
            float down = Mathf.Max(downTime, 0.0001f);
            float up = Mathf.Max(upTime, 0.0001f);

            if (phase >= down + up)
            {
                phase = -1f;
                transform.localPosition = home;
                return;
            }

            float amount = phase < down ? phase / down : 1f - (phase - down) / up;
            transform.localPosition = home + axis.normalized * (depth * amount);
        }
    }
}
