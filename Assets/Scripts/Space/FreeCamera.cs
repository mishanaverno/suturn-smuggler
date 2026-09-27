using Controls;
using DoublePrecision;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using UnityEngine;

namespace OuterSpace
{
    /// <summary>
    /// Отладочная камера: свободно летает по системе. F1 или ` (ё) — взять и отпустить.
    ///
    /// Своего космоса у неё нет: пока она взята, ExteriorView рисует внешние сцены от неё, а
    /// не от корабля, — начало отсчёта переезжает в её точку, оси становятся осями симуляции.
    /// Положение хранится в double, как у тел: на расстояниях системы float не годится.
    ///
    /// Камера стоит в глобальных осях и за телами не следует: на перемотке луны проносятся
    /// мимо, а корабль уходит из-под неё.
    ///
    /// Скорость не постоянная, а доля расстояния до ближайшей поверхности в секунду: в
    /// масштабах системы любая постоянная либо не сдвигает картинку вдали от тел, либо
    /// проскакивает станцию за кадр. Так подлёт к чему угодно замедляется сам.
    /// </summary>
    public class FreeCamera : MonoBehaviour
    {
        const double MinSpeed = 1.0;
        const double MinPace = 0.01;
        const double MaxPace = 10.0;
        const double PaceStep = 1.25;
        const double FastFactor = 10.0;

        public static FreeCamera instance { get; private set; }

        [Tooltip("Какую долю расстояния до ближайшей поверхности камера пролетает за секунду. Колесо меняет её в полёте.")]
        public double startPace = 0.3;
        public float lookSensitivity = 0.1f;
        [Tooltip("Скорость крена, °/с.")]
        public float rollSpeed = 60f;
        public float fieldOfView = 60f;

        public Camera Eye { get; private set; }
        public bool Active { get; private set; }
        /// <summary>Положение в глобальных осях симуляции, м.</summary>
        public Vector3d Position { get; private set; }

        double pace;
        double speed;
        GameInput.Context resume;
        CursorLockMode resumeLock;
        bool resumeVisible;

        void Awake()
        {
            instance = this;
            GameInput.Initialize();
            // Сама камера ничего не рисует: космос за неё рисуют камеры ExteriorView, а кабину
            // отсюда видно быть не должно. Она только задаёт взгляд и стоит поверх глаза
            // пилота, чтобы небо внешней сцены закрыло его кадр.
            Eye = GetComponent<Camera>();
            if (Eye == null) Eye = gameObject.AddComponent<Camera>();
            Eye.cullingMask = 0;
            Eye.clearFlags = CameraClearFlags.Depth;
            Eye.fieldOfView = fieldOfView;
            Eye.depth = 100f;
            Eye.enabled = false;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void Update()
        {
            if (GameInput.FreeCameraToggle.WasPressedThisFrame())
            {
                if (Active) Release();
                else Take();
            }
            if (!Active) return;
            Look();
            Fly();
        }

        /// <summary>Камера встаёт на место корабля и смотрит по его оси.</summary>
        void Take()
        {
            if (SimMono.playerShip is not Ship ship) return;
            Position = ship.simTransform.GLOBAL_R;
            transform.rotation = Quaternion.LookRotation(
                ExteriorView.ToHull(ship.attitude.Forward), ExteriorView.ToHull(ship.attitude.Up));
            pace = startPace;

            resume = GameInput.Current;
            resumeLock = Cursor.lockState;
            resumeVisible = Cursor.visible;
            GameInput.Switch(GameInput.Context.FreeCamera);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Eye.enabled = true;
            Active = true;
        }

        void Release()
        {
            GameInput.Switch(resume);
            Cursor.lockState = resumeLock;
            Cursor.visible = resumeVisible;
            Eye.enabled = false;
            Active = false;
        }

        /// <summary>Горизонта нет: мышь и крен вращают камеру в её собственных осях.</summary>
        void Look()
        {
            Vector2 look = GameInput.FreeLook.ReadValue<Vector2>() * lookSensitivity;
            float roll = GameInput.FreeRoll.ReadValue<float>() * rollSpeed * Time.unscaledDeltaTime;
            transform.rotation *= Quaternion.Euler(-look.y, look.x, roll);
        }

        /// <summary>
        /// Время реальное, а не симуляционное: на перемотке камера летает с той же скоростью,
        /// с какой летала без неё.
        /// </summary>
        void Fly()
        {
            float scroll = GameInput.FreeSpeed.ReadValue<float>();
            if (scroll != 0f) pace = Mathd.Clamp(pace * Mathd.Pow(PaceStep, Mathf.Sign(scroll)), MinPace, MaxPace);
            speed = Mathd.Max(pace * Clearance(), MinSpeed);

            Vector2 move = GameInput.FreeMove.ReadValue<Vector2>();
            Vector3 wish = transform.forward * move.y
                + transform.right * move.x
                + transform.up * GameInput.FreeVertical.ReadValue<float>();
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            double step = speed * (GameInput.FreeFast.IsPressed() ? FastFactor : 1.0) * Time.unscaledDeltaTime;
            Position += ExteriorView.FromHull(wish) * step;
        }

        /// <summary>Расстояние до ближайшей поверхности тела или до ближайшей станции, м.</summary>
        double Clearance()
        {
            double nearest = double.PositiveInfinity;
            foreach (SpaceObject body in SimMono.bodies)
                nearest = Mathd.Min(nearest, (body.simTransform.GLOBAL_R - Position).magnitude - body.radius);
            nearest = Mathd.Min(nearest, (SimMono.root.simTransform.GLOBAL_R - Position).magnitude - SimMono.root.radius);
            foreach (Station station in SimMono.stations)
                nearest = Mathd.Min(nearest, (station.simTransform.GLOBAL_R - Position).magnitude);
            return Mathd.Max(nearest, 0.0);
        }

        void OnGUI()
        {
            if (!Active) return;
            GUI.Label(new Rect(10f, 10f, 400f, 20f), $"FREE CAMERA  {speed:N0} m/s  pace {pace:0.###}/s  (F1 / ` — back)");
        }
    }
}
