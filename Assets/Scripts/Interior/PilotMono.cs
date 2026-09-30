using Controls;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Пилот на своём месте. Положение глаз задано точкой в кокпите и не меняется: человек
    /// пристёгнут, распоряжается он не телом, а взглядом и руками.
    ///
    /// Курсор видим, не захвачен и занят только одним делом: он указывает на органы
    /// управления. Голову поворачивают клавишами или мышью с зажатой правой кнопкой — тогда
    /// курсор прячется, а после отпускания встаёт в центр вида. Так курсор и взгляд не
    /// конкурируют: без правой кнопки ни одно движение мыши не сдвигает картинку.
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
        [Tooltip("Самое узкое поле зрения, до которого сводит колесо, °: разглядеть дальний прибор или точку за окном. Самое широкое — то, что стоит на камере.")]
        public float zoomFieldOfView = 20f;
        [Tooltip("Во сколько раз один щелчок колеса сужает или расширяет поле зрения.")]
        public float zoomStep = 1.15f;
        [Tooltip("Поворот головы мышью с зажатой правой кнопкой, градусов на пиксель при широком поле зрения. При увеличении — медленнее во столько же раз.")]
        public float glanceSpeed = 0.12f;
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
        bool glancing;

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

            if (GameInput.Focus.WasPressedThisFrame() && (hand == null || !hand.Grip(1))) Glance(true);
            if (glancing && GameInput.Focus.WasReleasedThisFrame()) Glance(false);
            Vector2 turn = GameInput.View.ReadValue<Vector2>() * (turnSpeed * Time.deltaTime);
            if (glancing) turn += GameInput.Glance.ReadValue<Vector2>() * (glanceSpeed * eye.fieldOfView / normalFieldOfView);
            yaw = Mathf.Clamp(yaw + turn.x, -yawLimit, yawLimit);
            pitch = Mathf.Clamp(pitch - turn.y, -pitchLimit, pitchLimit);
            eye.transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);

            if (hand != null)
            {
                if (GameInput.Click.WasPressedThisFrame() && !hand.Grip(0)) hand.Activate();
                if (GameInput.Click.WasReleasedThisFrame()) hand.Drop();
            }
            float wheel = GameInput.Scroll.ReadValue<float>();
            if (Mathf.Abs(wheel) > 0.01f)
            {
                int notch = wheel > 0f ? 1 : -1;
                if (hand == null || !hand.Scroll(notch))
                    eye.fieldOfView = Mathf.Clamp(eye.fieldOfView / Mathf.Pow(zoomStep, notch), zoomFieldOfView, normalFieldOfView);
            }
            // С идущей тягой место пилота не бросают: прожиг без присмотра некому отсечь.
            if (GameInput.Leave.WasPressedThisFrame() && !ControlBus.Read(SignalId.EngineRunning)) Release();
        }

        /// <summary>
        /// Голову ведёт мышь: курсор захвачен и спрятан. Отпустили — он встаёт в центр вида,
        /// туда, куда теперь смотрят. Confined, а не None: выпускать курсор из окна — терять нажатие.
        /// </summary>
        void Glance(bool on)
        {
            glancing = on;
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.Confined;
            Cursor.visible = !on;
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
            glancing = false;
            if (eye != null) eye.enabled = active;
            // Слух и рука необязательны: без AudioListener игра идёт молча, без Interactor —
            // молча же, но руками. Раньше их создавал код и они были всегда; теперь их
            // ставят в сцене, и отсутствие не должно валить переключение занятий.
            if (ear != null) ear.enabled = active;
            if (hand != null) hand.enabled = active;
        }
    }
}
