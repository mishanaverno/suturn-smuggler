using Controls;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Interior
{
    /// <summary>
    /// Пилот на своём месте. Положение глаз задано точкой в кокпите и не меняется: человек
    /// пристёгнут, распоряжается он не телом, а взглядом и руками.
    ///
    /// Курсор видим, не захвачен и занят только одним делом: он указывает на органы
    /// управления. Голову поворачивают клавишами. Так курсор и взгляд не конкурируют вовсе —
    /// ни одно движение мыши не сдвигает картинку, и вести курсор к дальней кнопке панели
    /// можно не глядя, куда при этом поедет голова.
    /// </summary>
    public class PilotMono : MonoBehaviour, IAim
    {
        public static PilotMono instance;

        /// <summary>
        /// Скорость поворота головы, градусов в секунду. Полный разворот от края до края
        /// кресла занимает около трёх секунд — голова, а не башня танка.
        /// </summary>
        public float turnSpeed = 55f;
        /// <summary>Предел разворота от исходного направления кресла.</summary>
        public float yawLimit = 120f;
        public float pitchLimit = 55f;
        public float reach = 1.6f;
        [Tooltip("Поле зрения, пока зажата правая кнопка, °: разглядеть дальний прибор или точку за окном.")]
        public float zoomFieldOfView = 20f;
        [Tooltip("За сколько взгляд доходит до точки под курсором по правому щелчку, с — примерно, поворот плавный.")]
        public float focusTime = 0.15f;
        /// <summary>Куда встаёт тело, когда игрок уходит с места.</summary>
        public Transform exit;
        [Tooltip("Начинать игру на месте пилота, а не телом в невесомости.")]
        public bool takeOverOnStart = true;

        [Tooltip("Камера на месте пилота: положение её трансформа и есть положение глаз.")]
        public Camera eye;
        [Tooltip("Рука пилота. Если пусто — будет взята с объекта камеры.")]
        public Interactor hand;
        public bool Active { get; private set; }

        public Ray Ray => eye.ScreenPointToRay(GameInput.Point.ReadValue<Vector2>());
        public float Reach => reach;
        public Vector2 LabelPosition => GameInput.Point.ReadValue<Vector2>();

        AudioListener ear;
        float normalFieldOfView;
        float yaw;
        float pitch;
        // Куда переводится взгляд, в осях кресла. Хранится направление, а не углы: курсор
        // едет вслед за этой точкой, и её экранное положение считается заново каждый кадр.
        Vector3 focus;
        bool focusing;

        void Awake()
        {
            instance = this;
            if (!BindEye()) return;
            SetActive(false);
        }

        /// <summary>
        /// Глаз, слух и рука живут в сцене, а не создаются кодом: их надо видеть, двигать
        /// и настраивать. Компонент только проверяет, что они на месте, и напоминает про
        /// единственное правило, которое нельзя нарушить, — слой симуляции глазами не виден
        /// вообще: это картинка навигационного прибора, а не космос. Космос за иллюминаторами
        /// рисует ExteriorView на своём слое.
        /// </summary>
        bool BindEye()
        {
            if (eye == null) eye = GetComponentInChildren<Camera>(true);
            if (eye == null)
            {
                Debug.LogError($"{GetType().Name} на «{name}»: не указана камера глаза.", this);
                enabled = false;
                return false;
            }
            ear = eye.GetComponent<AudioListener>();
            normalFieldOfView = eye.fieldOfView;
            if (hand == null) hand = eye.GetComponent<Interactor>();
            int simulation = LayerMask.NameToLayer("Simulation");
            if (simulation >= 0 && (eye.cullingMask & (1 << simulation)) != 0)
            {
                Debug.LogWarning($"Камера «{eye.name}» видит слой Simulation — это картинка " +
                    "навигационного прибора, а не вид наружу. Снимите слой в Culling Mask.", eye);
            }
            return true;
        }

        void Start()
        {
            if (takeOverOnStart) TakeOver();
        }

        void Update()
        {
            if (!Active) return;

            if (GameInput.Focus.WasPressedThisFrame())
            {
                focus = Seat * Ray.direction;
                focusing = true;
            }
            Vector2 turn = GameInput.View.ReadValue<Vector2>();
            if (turn != Vector2.zero) focusing = false;
            if (focusing) TurnToFocus();
            yaw = Mathf.Clamp(yaw + turn.x * turnSpeed * Time.deltaTime, -yawLimit, yawLimit);
            pitch = Mathf.Clamp(pitch - turn.y * turnSpeed * Time.deltaTime, -pitchLimit, pitchLimit);
            eye.transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
            eye.fieldOfView = GameInput.Focus.IsPressed() ? zoomFieldOfView : normalFieldOfView;
            // Курсор едет вместе с точкой, на которую указал: иначе после поворота он
            // показывал бы на что-то другое, а он — рука пилота на органах управления.
            if (focusing && Mouse.current != null)
                Mouse.current.WarpCursorPosition(eye.WorldToScreenPoint(eye.transform.position + Quaternion.Inverse(Seat) * focus));

            if (hand != null)
            {
                if (GameInput.Click.WasPressedThisFrame()) hand.Activate();
                if (GameInput.Click.WasReleasedThisFrame()) hand.Drop();
                float wheel = GameInput.Scroll.ReadValue<float>();
                if (Mathf.Abs(wheel) > 0.01f) hand.Scroll(wheel > 0f ? 1 : -1);
                for (int set = 0; set < GameInput.Grip.Length; set++)
                {
                    if (GameInput.Grip[set].WasPerformedThisFrame()) hand.Grip(set);
                }
            }
            if (GameInput.Leave.WasPressedThisFrame()) Release();
        }

        /// <summary>Поворот из мира в оси кресла — родителя глаза.</summary>
        Quaternion Seat => eye.transform.parent == null ? Quaternion.identity : Quaternion.Inverse(eye.transform.parent.rotation);

        /// <summary>
        /// Шаг к точке фокуса: доля оставшегося угла за кадр, так что поворот резкий в начале
        /// и мягкий в конце. За пределы разворота кресла голова не идёт — там и остановится.
        /// </summary>
        void TurnToFocus()
        {
            float targetYaw = Mathf.Clamp(Mathf.Atan2(focus.x, focus.z) * Mathf.Rad2Deg, -yawLimit, yawLimit);
            float targetPitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(focus.y, -1f, 1f)) * Mathf.Rad2Deg, -pitchLimit, pitchLimit);
            float k = 1f - Mathf.Exp(-3f * Time.deltaTime / Mathf.Max(focusTime, 1e-3f));
            yaw = Mathf.Lerp(yaw, targetYaw, k);
            pitch = Mathf.Lerp(pitch, targetPitch, k);
            if (Mathf.Abs(yaw - targetYaw) < 0.05f && Mathf.Abs(pitch - targetPitch) < 0.05f) focusing = false;
        }

        public void TakeOver()
        {
            if (Active) return;
            if (PlayerMono.instance != null) PlayerMono.instance.SetActive(false);
            yaw = 0f;
            pitch = 0f;
            SetActive(true);
            GameInput.Switch(GameInput.Context.Cockpit);
            // Захватывать курсор нечем и незачем, но выпускать его из окна — терять нажатие.
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
        }

        public void Release()
        {
            if (!Active) return;
            SetActive(false);
            if (PlayerMono.instance != null) PlayerMono.instance.SetActive(true, exit);
        }

        void SetActive(bool active)
        {
            Active = active;
            // Ни одна из трёх ссылок не обязана быть: камеру проверяет BindEye и ругается,
            // если её нет, а слух и рука необязательны по смыслу — без AudioListener игра
            // идёт молча, без Interactor молча же, но руками. Переключение занятий не должно
            // падать ни в одном из этих случаев: раньше код создавал всё сам и привык, что
            // всё всегда на месте.
            focusing = false;
            if (eye != null)
            {
                eye.enabled = active;
                eye.fieldOfView = normalFieldOfView;
            }
            // Слух и рука необязательны: без AudioListener игра идёт молча, без Interactor —
            // молча же, но руками. Раньше их создавал код и они были всегда; теперь их
            // ставят в сцене, и отсутствие не должно валить переключение занятий.
            if (ear != null) ear.enabled = active;
            if (hand != null) hand.enabled = active;
        }
    }
}
