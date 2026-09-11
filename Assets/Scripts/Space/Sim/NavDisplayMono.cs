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
        // Дальность - половина высоты видимой области в метрах. Лестница геометрическая:
        // равные шаги ручки дают равные множители, поэтому одно движение значит одно и то же
        // и у станции, и на масштабе всей системы. Шагов много и они мелкие - прибор с пятью
        // фиксированными дальностями заставлял прыгать через порядок там, где нужно чуть-чуть.
        public const double MinRange = 2.0e4;   // 20 км: рандеву, стыковка
        public const double MaxRange = 1.5e10;  // орбита Фебы, вся система
        public const int RangeSteps = 64;
        public const double DefaultRange = 1.3e9;
        // Сколько раз в секунду оставшийся разрыв сокращается в e раз. Дальность едет плавно,
        // а не прыгает: прыжок масштаба сбивает чтение картинки - глаз теряет, что где было.
        public const double ZoomSpeed = 9.0;

        public static NavDisplayMono instance;

        public int textureWidth = 1024;
        public int textureHeight = 768;
        // 12 пикселей из 768 по высоте экрана прибора.
        public double markerFraction = 12.0 / 768.0;
        public double labelFraction = 18.0 / 768.0;
        public double lineFraction = 2.0 / 768.0;
        // Поле переименовано намеренно: в сценах лежит старый индекс из лестницы на пять
        // ступеней, и на новой он означал бы совсем другую дальность.
        public int rangeStep = -1;

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

        double range = DefaultRange;

        /// <summary>Дальность, на которую прибор едет: та, что выбрана ручкой.</summary>
        public double TargetRange => RangeAt(rangeStep);
        /// <summary>Дальность, которую прибор показывает сейчас.</summary>
        public double Range => range;

        public static double RangeAt(int step)
        {
            double t = System.Math.Clamp(step / (double)(RangeSteps - 1), 0.0, 1.0);
            return MinRange * System.Math.Pow(MaxRange / MinRange, t);
        }

        /// <summary>Ближайший шаг лестницы к заданной дальности.</summary>
        public static int StepAt(double rangeMeters)
        {
            double t = System.Math.Log(rangeMeters / MinRange) / System.Math.Log(MaxRange / MinRange);
            return Mathf.Clamp(Mathf.RoundToInt((float)t * (RangeSteps - 1)), 0, RangeSteps - 1);
        }
        public double MetersPerPixel => NavScale.MetersPerPixel(NavScale.OrthographicSize, SimView.metersPerSceneUnit, textureHeight);
        public double MarkerSceneDiameter => NavScale.MarkerSceneDiameter(markerFraction, NavScale.OrthographicSize);
        public double LabelSceneHeight => NavScale.MarkerSceneDiameter(labelFraction, NavScale.OrthographicSize);
        public float LineSceneWidth => (float)NavScale.MarkerSceneDiameter(lineFraction, NavScale.OrthographicSize);

        void Awake()
        {
            instance = this;
            if (rangeStep < 0) rangeStep = StepAt(DefaultRange);
            range = TargetRange;
            SimView.metersPerSceneUnit = NavScale.MetersPerSceneUnit(range);

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

            CreateLabelRail();
            CreateDebugView();
        }

        /// <summary>
        /// Подписи имён — на слое симуляции: их рисует камера прибора, а не глаза пилота.
        ///
        /// Рельса живёт в корне сцены, а не в детях дисплея, и это не вкусовщина: сам
        /// NavDisplayMono сидит на UI-объекте с RectTransform внутри канваса, а текст,
        /// оказавшийся в UI-иерархии, рисуется по чужим правилам и чаще всего не рисуется
        /// вовсе. Линии выносок это переживали — LineRenderer в мировом режиме трансформ
        /// родителя игнорирует, — и симптом выглядел как «выноски есть, подписей нет».
        /// Всё, что раскладывается в мировых координатах, держим вне канваса.
        /// </summary>
        void CreateLabelRail()
        {
            GameObject railObject = new("NavLabelRail");
            railObject.layer = LayerMask.NameToLayer("Simulation");
            railObject.transform.SetParent(null);
            railObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            railObject.transform.localScale = Vector3.one;
            railObject.AddComponent<NavLabelRail>();
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
        /// Прибор переезжает на своё место в рубке: тот же самый холст, но в мире, на корпусе
        /// монитора. Ничего, кроме способа вывода, не меняется — экран и раньше был прибором,
        /// просто висел на весь кадр за неимением рубки.
        /// </summary>
        public void MountOn(Transform anchor, double screenWidthMeters)
        {
            Canvas canvas = readout.canvas;
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rect = canvas.GetComponent<RectTransform>();
            rect.SetParent(anchor, false);
            rect.sizeDelta = new Vector2(textureWidth, textureHeight);
            rect.localPosition = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            float scale = (float)(screenWidthMeters / textureWidth);
            rect.localScale = new Vector3(scale, scale, scale);
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

            focus ??= SimMono.playerShip.simTransform;
            SimView.origin = focus.GLOBAL_R;
            // Догоняем выбранную дальность геометрически: в логарифме это обычное
            // экспоненциальное сглаживание, а на экране - равномерный наезд.
            range *= System.Math.Pow(TargetRange / range, 1.0 - System.Math.Exp(-ZoomSpeed * Time.deltaTime));
            SimView.metersPerSceneUnit = NavScale.MetersPerSceneUnit(range);
            foreach (SpaceObject obj in SimMono.updateOrder) obj.simTransform.Reproject();

            cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            cam.transform.position = cam.transform.rotation * Vector3.back * 100f;
            UpdateReadout();
        }

        /// <summary>Дальность переключается ступенями: набор запомнен, подписи честные.</summary>
        public void ShiftRange(int steps)
            => rangeStep = Mathf.Clamp(rangeStep + steps, 0, RangeSteps - 1);

        /// <summary>Разворот вида прибора. Тангаж ограничен, чтобы не проходить через полюс.</summary>
        public void Rotate(Vector2 degrees)
        {
            yaw += degrees.x;
            pitch = Mathf.Clamp(pitch + degrees.y, -89f, 89f);
        }

        public void CycleTarget()
        {
            targetIndex++;
            if (targetIndex >= SimMono.bodies.Count) targetIndex = -1;
            SimMono.target = targetIndex < 0 ? null : SimMono.bodies[targetIndex];
        }

        public void CycleFocus()
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
