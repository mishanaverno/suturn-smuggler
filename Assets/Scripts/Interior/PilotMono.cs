using Controls;
using UnityEngine;

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
        public float yawLimit = 80f;
        public float pitchLimit = 55f;
        public float reach = 1.6f;
        /// <summary>Куда встаёт тело, когда игрок уходит с места.</summary>
        public Transform exit;

        public Camera eye { get; private set; }
        public bool Active { get; private set; }

        public Ray Ray => eye.ScreenPointToRay(GameInput.Point.ReadValue<Vector2>());
        public float Reach => reach;
        public Vector2 LabelPosition => GameInput.Point.ReadValue<Vector2>();

        Interactor hand;
        AudioListener ear;
        float yaw;
        float pitch;

        void Awake()
        {
            instance = this;

            GameObject eyeObject = new("Eye");
            eyeObject.transform.SetParent(transform, false);
            eye = eyeObject.AddComponent<Camera>();
            eye.nearClipPlane = 0.03f;
            eye.farClipPlane = 500f;
            eye.cullingMask = ~(1 << LayerMask.NameToLayer("Simulation"));
            ear = eyeObject.AddComponent<AudioListener>();
            hand = eyeObject.AddComponent<Interactor>();

            SetActive(false);
        }

        void Update()
        {
            if (!Active) return;

            Vector2 turn = GameInput.View.ReadValue<Vector2>();
            yaw = Mathf.Clamp(yaw + turn.x * turnSpeed * Time.deltaTime, -yawLimit, yawLimit);
            pitch = Mathf.Clamp(pitch - turn.y * turnSpeed * Time.deltaTime, -pitchLimit, pitchLimit);
            eye.transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);

            if (GameInput.Click.WasPressedThisFrame()) hand.Activate();

            float wheel = GameInput.Scroll.ReadValue<float>();
            if (Mathf.Abs(wheel) > 0.01f) hand.Scroll(wheel > 0f ? 1 : -1);
            if (GameInput.Leave.WasPressedThisFrame()) Release();
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
            eye.enabled = active;
            ear.enabled = active;
            hand.enabled = active;
        }
    }
}
