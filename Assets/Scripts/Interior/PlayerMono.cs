using Controls;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Тело вне кокпита. Оно не ходит: на корабле невесомость, пола нет, и «низ» существует
    /// только потому, что так поставлены переборки.
    ///
    /// Движение — не ходьба с другой константой, а другая модель: тяга разгоняет, а само
    /// ничто не тормозит. Гасить скорость приходится намеренно. Небольшое затухание всё же
    /// есть, и это не поблажка физике, а двигатели ориентации скафандра, работающие сами:
    /// без них любое касание переборки отправляло бы игрока в неуправляемый полёт через
    /// половину корабля, и починка узла превращалась бы в борьбу с управлением.
    ///
    /// CharacterController здесь только ради столкновений: скорость считается сама,
    /// тяготения нет, Move вызывается уже готовым вектором.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMono : MonoBehaviour, IAim
    {
        public static PlayerMono instance;

        /// <summary>Тяга скафандра, м/с². Человек в невесомости разгоняется медленно.</summary>
        public float thrust = 2.5f;
        public float boost = 2f;
        /// <summary>Предел скорости: быстрее внутри корабля не летают, там переборки.</summary>
        public float maxSpeed = 3f;
        /// <summary>Постоянная работа двигателей ориентации, доля скорости в секунду.</summary>
        public float damping = 0.6f;
        /// <summary>Гашение по команде — резкое.</summary>
        public float brakeDamping = 6f;
        public float lookSensitivity = 0.07f;
        public float reach = 1.6f;

        [Tooltip("Камера глаз. Высота взгляда задаётся положением её трансформа в сцене.")]
        public Camera eye;
        [Tooltip("Рука. Если пусто — будет взята с объекта камеры.")]
        public Interactor hand;
        public bool Active { get; private set; } = true;

        public Ray Ray => new(eye.transform.position, eye.transform.forward);
        public float Reach => reach;
        public Vector2 LabelPosition => new(Screen.width * 0.5f, Screen.height * 0.5f);

        CharacterController controller;
        AudioListener ear;
        Vector3 velocity;
        float yaw;
        float pitch;

        void Awake()
        {
            instance = this;
            controller = GetComponent<CharacterController>();
            if (!BindEye()) return;
            yaw = transform.eulerAngles.y;
        }

        /// <summary>
        /// Тело оживает само, если никто не занял место пилота. Решение принимается по
        /// намерению пилота, а не по его состоянию: порядок Start у разных объектов не
        /// определён, и проверка «уже сидит?» зависела бы от того, кто проснулся первым.
        /// </summary>
        void Start()
        {
            bool pilotTakesOver = PilotMono.instance != null && PilotMono.instance.takeOverOnStart;
            if (!pilotTakesOver) SetActive(true);
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
            if (hand == null) hand = eye.GetComponent<Interactor>();
            int simulation = LayerMask.NameToLayer("Simulation");
            if (simulation >= 0 && (eye.cullingMask & (1 << simulation)) != 0)
            {
                Debug.LogWarning($"Камера «{eye.name}» видит слой Simulation — это картинка " +
                    "навигационного прибора, а не вид наружу. Снимите слой в Culling Mask.", eye);
            }
            return true;
        }

        void Update()
        {
            if (!Active) return;
            ReadLook();
            ReadThrust();
            if (hand != null && GameInput.Interact.WasPressedThisFrame()) hand.Activate();
            if (hand != null && GameInput.Interact.WasReleasedThisFrame()) hand.Drop();
        }

        void ReadLook()
        {
            Vector2 look = GameInput.Look.ReadValue<Vector2>() * lookSensitivity;
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, -89f, 89f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            eye.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void ReadThrust()
        {
            Vector2 move = GameInput.Move.ReadValue<Vector2>();
            float vertical = GameInput.Vertical.ReadValue<float>();
            // Тяга идёт туда, куда смотришь: в невесомости «вперёд» — это взгляд, а не пол.
            Vector3 wish = eye.transform.forward * move.y
                + eye.transform.right * move.x
                + transform.up * vertical;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            float power = thrust * (GameInput.Sprint.IsPressed() ? boost : 1f);
            velocity += wish * (power * Time.deltaTime);

            float drag = GameInput.Brake.IsPressed() ? brakeDamping : damping;
            velocity *= Mathf.Exp(-drag * Time.deltaTime);
            velocity = Vector3.ClampMagnitude(velocity, maxSpeed);

            CollisionFlags hits = controller.Move(velocity * Time.deltaTime);
            // Столкновение гасит скорость, а не отражает: человек не мячик, он цепляется
            // за переборку рукой.
            if (hits != CollisionFlags.None) velocity *= 0.2f;
        }

        /// <summary>Тело замирает, пока игрок сидит на месте пилота, и оживает, когда встаёт.</summary>
        public void SetActive(bool active, Transform at = null)
        {
            Active = active;
            if (at != null)
            {
                controller.enabled = false;
                transform.SetPositionAndRotation(at.position, Quaternion.Euler(0f, at.eulerAngles.y, 0f));
                yaw = at.eulerAngles.y;
                pitch = 0f;
            }
            velocity = Vector3.zero;
            controller.enabled = active;
            // Ни одна из трёх ссылок не обязана быть: камеру проверяет BindEye и ругается,
            // если её нет, а слух и рука необязательны по смыслу — без AudioListener игра
            // идёт молча, без Interactor молча же, но руками. Переключение занятий не должно
            // падать ни в одном из этих случаев: раньше код создавал всё сам и привык, что
            // всё всегда на месте.
            if (eye != null) eye.enabled = active;
            // Слух и рука необязательны: без AudioListener игра идёт молча, без Interactor —
            // молча же, но руками. Раньше их создавал код и они были всегда; теперь их
            // ставят в сцене, и отсутствие не должно валить переключение занятий.
            if (ear != null) ear.enabled = active;
            if (hand != null) hand.enabled = active;
            if (active)
            {
                GameInput.Switch(GameInput.Context.Bridge);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
