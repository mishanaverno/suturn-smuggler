using System.Collections.Generic;
using Game;
using OuterSpace.Sim.Objects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OuterSpace.Sim
{
    /// <summary>
    /// Экран навигационного компьютера корабля: прибор внутри игрового мира, а не свободный
    /// вид космоса. Своя камера, своя текстура фиксированного разрешения, свой ввод.
    /// Пока рубки нет, текстура выводится отладочным полноэкранным видом; когда появится
    /// панель, та же текстура вешается на неё и здесь ничего не меняется.
    ///
    /// Камера ортографическая: в перспективе две одинаковые метки на разной глубине имели бы
    /// разный экранный размер, и промежуточный размер снова начал бы что-то означать.
    /// </summary>
    public class NavDisplayMono : MonoBehaviour
    {
        // Дальность - половина высоты видимой области в метрах. Фиксированный набор удобнее
        // плавного зума: игрок запоминает диапазоны, а подписи под ними честные.
        public static readonly double[] Ranges = {
            1.3e10, // вся система, орбита Фебы
            3.7e9,  // до Япета
            1.3e9,  // до Титана
            5.0e7,  // сфера влияния Титана
            5.0e6,  // окрестности Титана
        };
        public const double DefaultRange = 1.3e9;

        public static NavDisplayMono instance;

        public int textureWidth = 1024;
        public int textureHeight = 768;
        // 12 пикселей из 768 по высоте экрана прибора.
        public double markerFraction = 12.0 / 768.0;
        public double labelFraction = 18.0 / 768.0;
        public double lineFraction = 2.0 / 768.0;
        public int rangeIndex = 2;

        public RenderTexture texture { get; private set; }
        public TextMeshProUGUI readout { get; private set; }
        public Camera cam { get; private set; }
        /// <summary>
        /// Точка, на которой стоит начало сцены: вокруг неё вращается вид. Не обязательно тело —
        /// на точку манёвра смотрят не реже, чем на луны.
        /// </summary>
        public SimTransform focus;

        float yaw = 0f;
        float pitch = 60f;
        int focusIndex = -1;
        int targetIndex = -1;

        public double Range => Ranges[rangeIndex];
        public double MetersPerPixel => NavScale.MetersPerPixel(NavScale.OrthographicSize, SimView.metersPerSceneUnit, textureHeight);
        public double MarkerSceneDiameter => NavScale.MarkerSceneDiameter(markerFraction, NavScale.OrthographicSize);
        public double LabelSceneHeight => NavScale.MarkerSceneDiameter(labelFraction, NavScale.OrthographicSize);
        public float LineSceneWidth => (float)NavScale.MarkerSceneDiameter(lineFraction, NavScale.OrthographicSize);

        void Awake()
        {
            instance = this;
            SimView.metersPerSceneUnit = NavScale.MetersPerSceneUnit(Range);

            texture = new RenderTexture(textureWidth, textureHeight, 24) { name = "NavDisplay" };

            GameObject camObject = new("NavCamera");
            camObject.transform.parent = transform;
            cam = camObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = (float)NavScale.OrthographicSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            // Прибор видит только слой симуляции, и ничего кроме него нарисовать на нём
            // технически невозможно.
            cam.cullingMask = 1 << LayerMask.NameToLayer("Simulation");
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 200f;
            cam.targetTexture = texture;

            CreateDebugView();
        }

        void CreateDebugView()
        {
            GameObject canvasObject = new("NavDisplayDebugView");
            canvasObject.transform.parent = transform;
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100;

            GameObject imageObject = new("Screen");
            imageObject.transform.SetParent(canvasObject.transform, false);
            RawImage image = imageObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            GameObject textObject = new("Readout");
            textObject.transform.SetParent(canvasObject.transform, false);
            readout = textObject.AddComponent<TextMeshProUGUI>();
            readout.fontSize = 18f;
            readout.raycastTarget = false;
            RectTransform textRect = readout.rectTransform;
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(0f, 1f);
            textRect.pivot = new Vector2(0f, 1f);
            textRect.anchoredPosition = new Vector2(12f, -12f);
            textRect.sizeDelta = new Vector2(600f, 220f);
        }

        /// <summary>
        /// Числа, без которых управление читается как случайное: в каком режиме стоит корабль,
        /// насколько двигает орбиту одно нажатие и почему перемотка идёт медленнее запрошенной.
        /// </summary>
        void UpdateReadout()
        {
            Ship ship = SimMono.playerShip as Ship;
            if (ship == null) return;
            GameMono game = GameMono.instance;
            string target = SimMono.target == null ? "NONE" : SimMono.target.GameObject.name;
            string warp = $"WARP x{game.WarpSpeed:0.##} / x{game.TimeSpeed}";
            if (game.WarpLimitReason != null) warp += $"\nSLOWDOWN: {game.WarpLimitReason}";
            readout.text =
                $"ORIENT {ship.orientation.ToString().ToUpperInvariant()}   TARGET {target}\n" +
                $"STEP {ship.CurrentTimeStep:F0} s   {ship.CurrentSpeedStep:F2} m/s\n" +
                warp + ManeuverReadout(ship, game.Epoch);
        }

        /// <summary>
        /// По этим числам игрок и решает, когда включать двигатель и когда выключать: остаток
        /// характеристической скорости, сколько его ещё жечь, отсчёты до начала прожига и до
        /// узла, и расхождение фактической орбиты с плановой, которое стремится к нулю.
        /// </summary>
        static string ManeuverReadout(Ship ship, double epoch)
        {
            Maneuver maneuver = ship.GetManeuver();
            if (maneuver == null) return "";
            (double periapsis, double apoapsis) = AstroDynamic.GetPeriapsisAndApoapsis(ship.orbitParams);
            (double plannedPeriapsis, double plannedApoapsis) = AstroDynamic.GetPeriapsisAndApoapsis(maneuver.newOrbitParams);
            return
                $"\nDV {ship.RemainingDeltaV:F1} / {maneuver.PlannedMagnitude:F1} m/s   " +
                $"BURN {TrajectoryRenderer.Clock(ship.RemainingBurnDuration)}\n" +
                $"IGNITION T-{TrajectoryRenderer.Clock(ship.BurnStartEpoch - epoch)}   " +
                $"NODE MT+{TrajectoryRenderer.Clock(maneuver.startEpoch - epoch)}\n" +
                $"DPE {(periapsis - plannedPeriapsis) / 1000.0:F1} km   " +
                $"DAP {(apoapsis - plannedApoapsis) / 1000.0:F1} km";
        }

        // Update, а не LateUpdate: SimMono двигает тела в FixedUpdate, то есть до Update
        // текущего кадра, а орбиты и сферы влияния строятся в LateUpdate. Перепроецировать
        // сцену надо между тем и другим, иначе линии окажутся построены в старом масштабе.
        void Update()
        {
            if (SimMono.playerShip == null) return;
            ReadInput();

            focus ??= SimMono.playerShip.simTransform;
            SimView.origin = focus.GLOBAL_R;
            SimView.metersPerSceneUnit = NavScale.MetersPerSceneUnit(Range);
            foreach (SpaceObject obj in SimMono.updateOrder) obj.simTransform.Reproject();

            cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            cam.transform.position = cam.transform.rotation * Vector3.back * 100f;
            UpdateReadout();
        }

        void ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.LeftBracket)) rangeIndex = Mathf.Max(rangeIndex - 1, 0);
            if (Input.GetKeyDown(KeyCode.RightBracket)) rangeIndex = Mathf.Min(rangeIndex + 1, Ranges.Length - 1);

            if (Input.GetKey(KeyCode.LeftArrow)) yaw -= 60f * Time.deltaTime;
            if (Input.GetKey(KeyCode.RightArrow)) yaw += 60f * Time.deltaTime;
            if (Input.GetKey(KeyCode.UpArrow)) pitch = Mathf.Min(pitch + 60f * Time.deltaTime, 89f);
            if (Input.GetKey(KeyCode.DownArrow)) pitch = Mathf.Max(pitch - 60f * Time.deltaTime, -89f);

            if (Input.GetKeyDown(KeyCode.Tab)) CycleFocus();
            if (Input.GetKeyDown(KeyCode.T)) CycleTarget();
        }

        void CycleTarget()
        {
            targetIndex++;
            if (targetIndex >= SimMono.bodies.Count) targetIndex = -1;
            SimMono.target = targetIndex < 0 ? null : SimMono.bodies[targetIndex];
        }

        void CycleFocus()
        {
            List<SimTransform> points = FocusPoints();
            focusIndex++;
            if (focusIndex >= points.Count) focusIndex = -1;
            focus = focusIndex < 0 ? SimMono.playerShip.simTransform : points[focusIndex];
        }

        static List<SimTransform> FocusPoints()
        {
            List<SimTransform> points = new();
            foreach (SpaceObject obj in SimMono.updateOrder) points.Add(obj.simTransform);
            Maneuver maneuver = (SimMono.playerShip as Ship)?.GetManeuver();
            if (maneuver != null) points.Add(maneuver.simTransform);
            return points;
        }
    }
}
