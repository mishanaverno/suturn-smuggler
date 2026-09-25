using System;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Орган, который крутят колесом мыши, наведя на него курсор. Отдельным интерфейсом,
    /// потому что вращение — не «много нажатий»: у него есть знак и нет состояния «держат».
    /// </summary>
    public interface IScrollable
    {
        /// <summary>Щелчок колеса: +1 или −1.</summary>
        void Scroll(int direction);
    }

    /// <summary>
    /// Крутилка. Колесо под курсором — один щелчок, один шаг: величина меняется ровно
    /// настолько, насколько повернули, и не убегает, пока рука лежит на органе.
    ///
    /// Тем и лучше рычага с удержанием, который тут был раньше: у рычага величина ползёт,
    /// пока держишь, и чтобы попасть в нужное значение, надо отпустить вовремя. Крутилка
    /// не требует чувства времени — только счёта.
    ///
    /// Поэтому и разгон здесь считается по счёту, а не по скорости вращения: щелчки подряд
    /// в одну сторону делают шаг крупнее, и одна и та же серия щелчков всегда даёт один и
    /// тот же результат, как бы быстро её ни крутили. Разгон от частоты вернул бы ту самую
    /// зависимость от темпа руки, ради ухода от которой крутилку и завели.
    ///
    /// Обратный щелчок сбрасывает разгон сразу: перелетел — крутишь назад и попадаешь в
    /// мелкий шаг, не отрывая руки. Поэтому отдельного «точного режима» органу не нужно.
    ///
    /// Величины крутилка не знает: она отдаёт знак, а насколько сдвинуть — решает устройство.
    /// Что получилось, человек читает на табло рядом, а не по положению ручки: «правильного»
    /// положения у ручки нет, она считает щелчки.
    ///
    /// Компонент живёт на корне органа, вращаемая деталь — в поле part. Щелчок настоящей
    /// ручки — рывок с доводкой, поэтому поворот идёт по кривой, а не с одной скоростью;
    /// пружинку с перелётом рисуют той же кривой, уходящей выше единицы.
    /// </summary>
    public class KnobControl : MonoBehaviour, IInteractable, IScrollable
    {
        [Tooltip("Что эта крутилка крутит. Ассет из папки разъёмов.")]
        public StepPort port;

        [Tooltip("Вращаемая деталь.")]
        public Transform part;

        [Tooltip("Вокруг чего вращается, в местных осях детали. Нормализуется.")]
        public Vector3 axis = Vector3.up;

        [Tooltip("Градусов за один щелчок.")]
        public float degreesPerStep = 24f;

        [Tooltip("За сколько секунд отрабатывается один щелчок.")]
        public float stepTime = 0.09f;

        [Tooltip("Как идёт поворот внутри щелчка: 0 — начало, 1 — конец. Выше единицы — перелёт с возвратом.")]
        public AnimationCurve motion = new(new Keyframe(0f, 0f, 0f, 3f), new Keyframe(1f, 1f, 0f, 0f));

        [Tooltip("Ограничить поворот. Ручка с упорами не крутится дальше края, даже если щелчки идут.")]
        public bool limited;

        public float minAngle = -120f;
        public float maxAngle = 120f;

        [Tooltip("Подпись для старой проводки по имени. При заполненном разъёме не используется.")]
        public string label = "";

        [Tooltip("Через сколько щелчков подряд в одну сторону шаг растёт вдесятеро. 0 — крутилка без разгона.")]
        public int stepsPerDecade = 5;

        [Tooltip("Насколько шаг может вырасти по сравнению с одиночным щелчком.")]
        public int maxScale = 100;

        [Tooltip("Пауза, после которой счёт щелчков начинается заново, с.")]
        public float resetDelay = 0.5f;

        [HideInInspector] public Action<int> onStep;
        [HideInInspector] public Func<bool> available;

        ControlResponse[] responses;
        int run;
        int lastDirection;
        float lastTime;
        Quaternion home;
        float from;
        float target;
        float angle;
        float phase = -1f;

        void Awake()
        {
            responses = GetComponents<ControlResponse>();
            home = part.localRotation;
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
            part.localRotation = home * Quaternion.AngleAxis(angle, axis.normalized);
        }

        public string Prompt => port != null ? port.Title : label;

        public bool Available => port != null
            ? ControlBus.Available(port.id)
            : onStep != null && (available == null || available());

        public bool Wired => (port != null && port.Assigned) || onStep != null;

        /// <summary>Крутилку не нажимают: щелчок по ней ничего не значит.</summary>
        public void Interact() { }

        public void Scroll(int direction)
        {
            if (!Available || direction == 0) return;

            int sign = direction > 0 ? 1 : -1;
            int step = sign * Scale(sign);

            if (port != null) ControlBus.Turn(port.id, step);
            else onStep(step);

            // Ручка поворачивается на один щелчок независимо от разгона: крутанули один раз —
            // видно один раз, а насколько это оказалось много, читается на табло. Щелчок
            // начинается с того места, где ручка сейчас, а не с прошлой цели: иначе быстрая
            // серия щелчков рвала бы движение.
            from = angle;
            target += degreesPerStep * sign;
            if (limited) target = Mathf.Clamp(target, minAngle, maxAngle);
            phase = 0f;
            foreach (ControlResponse response in responses) response.Play(sign);
        }

        /// <summary>
        /// Во сколько раз крупнее одиночного щелчка идёт этот. Счёт ведётся здесь, а не в
        /// устройстве: разгон — свойство руки на конкретной ручке, и разогнанная ручка Δv
        /// не должна разгонять соседнюю.
        /// </summary>
        int Scale(int sign)
        {
            if (sign != lastDirection || Time.unscaledTime - lastTime > resetDelay) run = 0;
            lastDirection = sign;
            lastTime = Time.unscaledTime;
            run++;

            if (stepsPerDecade <= 0) return 1;
            int scale = 1;
            for (int decade = (run - 1) / stepsPerDecade; decade > 0 && scale * 10 <= maxScale; decade--) scale *= 10;
            return scale;
        }
    }
}
