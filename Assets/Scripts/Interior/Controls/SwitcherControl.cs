using System;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Галетный переключатель: ручка с несколькими положениями, каждое — своя команда.
    /// Щелчок колеса переводит на соседнее положение по кругу и выполняет его команду.
    ///
    /// Колесом, а не нажатием, потому что у ручки есть направление: крутят её в обе стороны,
    /// и проскочившее положение возвращают тем же движением руки, а не полным кругом.
    ///
    /// Разгона, в отличие от крутилки, нет: положений мало, и щелчок колеса всегда значит
    /// ровно одно положение.
    ///
    /// Если команда соседнего положения сейчас недоступна, ручка остаётся на месте:
    /// переключатель, вставший на положение, которое ничего не сделало, врал бы глазам.
    ///
    /// Компонент живёт на корне органа, ручка — в поле part. У ручки есть место, где она
    /// должна стоять, и идёт она туда, откуда бы ни начинала. Углы задаются по одному на
    /// положение: у настоящих галет шаг бывает неравным, и подбирать его удобнее глазами.
    /// Из последнего положения в первое ручка идёт обратно через все промежуточные углы,
    /// как галета с упором.
    /// </summary>
    public class SwitcherControl : MonoBehaviour, IInteractable, IScrollable
    {
        const int MaxPositions = 6;

        [Tooltip("Команды положений по порядку, не больше шести. Первое — положение при запуске.")]
        public CommandPort[] ports = Array.Empty<CommandPort>();

        [Tooltip("Ручка переключателя.")]
        public Transform part;

        [Tooltip("Вокруг чего вращается, в местных осях ручки. Нормализуется.")]
        public Vector3 axis = Vector3.up;

        [Tooltip("Угол каждого положения от исходного поворота, градусы. По одному на команду.")]
        public float[] angles = { 0f, 45f };

        [Tooltip("За сколько секунд ручка доходит до положения.")]
        public float stepTime = 0.09f;

        [Tooltip("Как идёт поворот: 0 — начало, 1 — конец. Выше единицы — перелёт с возвратом.")]
        public AnimationCurve motion = new(new Keyframe(0f, 0f, 0f, 3f), new Keyframe(1f, 1f, 0f, 0f));

        ControlResponse[] responses;
        int position;
        Quaternion home;
        float from;
        float target;
        float angle;
        float phase = -1f;

        void Awake()
        {
            responses = GetComponents<ControlResponse>();
            home = part.localRotation;
            from = target = angle = angles[0];
            Apply();
        }

        void OnValidate()
        {
            if (ports.Length > MaxPositions) Array.Resize(ref ports, MaxPositions);
        }

        /// <summary>Подпись — положение, в котором ручка стоит: куда её повернут, решает рука.</summary>
        public string Prompt => ports.Length == 0 || ports[position] == null ? name : ports[position].Title;

        /// <summary>Доступна, пока есть куда повернуть хотя бы в одну сторону.</summary>
        public bool Available => Ready(1) || Ready(-1);

        public bool Wired => Array.Exists(ports, port => port != null && port.Assigned);

        /// <summary>Галету не нажимают: щелчок по ней ничего не значит, её крутят.</summary>
        public void Interact() { }

        public void Scroll(int direction)
        {
            if (direction == 0) return;
            int sign = direction > 0 ? 1 : -1;
            if (!Ready(sign)) return;

            position = Step(sign);
            ControlBus.Invoke(ports[position].id);

            from = angle;
            target = angles[Mathf.Clamp(position, 0, angles.Length - 1)];
            phase = 0f;
            foreach (ControlResponse response in responses) response.Play(position);
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

        void Apply() => part.localRotation = home * Quaternion.AngleAxis(angle, axis.normalized);

        bool Ready(int sign)
        {
            if (ports.Length == 0) return false;
            CommandPort next = ports[Step(sign)];
            return next != null && ControlBus.Available(next.id);
        }

        int Step(int sign) => (position + sign + ports.Length) % ports.Length;
    }
}
