using System.Collections.Generic;
using System.Text;
using Game;
using OuterSpace.Sim.Objects;
using TMPro;
using UnityEngine;
using Utilities;
using Interior;

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
    public class NavDisplayPanel : MonoBehaviour
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

        /// <summary>Угол стекла, к которому прижаты показания.</summary>
        public enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

        public static NavDisplayPanel instance;

        [Tooltip("Разрешение прибора по высоте. Ширина считается из пропорций стекла.")]
        public int textureHeight = 768;
        [Tooltip("Ширина текстуры. Подгоняется под меш при запуске; заданное здесь значение — запас на случай, если поверхности нет.")]
        public int textureWidth = 1024;
        // Размеры элементов прибора задаются в пикселях его текстуры — в том же счёте, в
        // котором задано разрешение выше. Это единственная мера, которую видно глазом:
        // «метка 12 пикселей» проверяется на скриншоте, а доля экрана — нет. Внутри всё
        // равно считается долей высоты (пиксели / textureHeight), потому что сам экран может
        // быть любого размера в метрах.
        [Tooltip("Диаметр метки тела в пикселях текстуры.")]
        public float markerPixels = 12f;
        [Tooltip("Высота подписи имени в пикселях текстуры. Кегль в образце подписи на размер не влияет — он нормализуется.")]
        public float labelPixels = 18f;
        [Tooltip("Высота строки показаний в пикселях текстуры. Кегль в образце строки на размер не влияет — он нормализуется.")]
        public float readoutPixels = 18f;
        [Tooltip("Толщина линий в пикселях текстуры: орбиты, траектории, выноски, кольца меток.")]
        public float linePixels = 2f;

        [Header("Maneuver colors")]
        [Tooltip("Цвет траектории первого, ближайшего манёвра.")]
        public Color maneuver1Color = new(0.75f, 0.45f, 1f);
        [Tooltip("Цвет траектории второго манёвра.")]
        public Color maneuver2Color = new(1f, 0.55f, 0.2f);
        [Tooltip("Цвет траектории третьего манёвра.")]
        public Color maneuver3Color = new(0.25f, 1f, 0.55f);
        // Поле переименовано намеренно: в сценах лежит старый индекс из лестницы на пять
        // ступеней, и на новой он означал бы совсем другую дальность.
        public int rangeStep = -1;

        /// <summary>Камера прибора. Создаётся при запуске — см. CreateCamera.</summary>
        public Camera cam { get; private set; }

        // Всё, что прибор сделал сам. Объекты — его дети и умрут вместе с ним; список нужен
        // на случай, когда сносят один компонент, а объект остаётся жить.
        readonly System.Collections.Generic.List<GameObject> made = new();
        [Tooltip("Поверхность, на которой видна картинка прибора: любой меш с UV.")]
        public Renderer surface;
        [Tooltip("Чем залит экран там, где ничего нет.")]
        public Color screenBackground = Color.black;
        [Tooltip("Растянуть развёртку экрана на всю текстуру. Нужно, если меш вырезан из модели и его UV — кусок общей развёртки.")]
        public bool normalizeScreenUV = true;
        [Tooltip("Повернуть картинку на стекле. Развёртка вырезанной грани может идти вдоль любой стороны.")]
        public ScreenTurn screenRotation = ScreenTurn.Deg0;
        [Tooltip("Отразить картинку поперёк.")]
        public bool flipScreenU;
        [Tooltip("Отразить картинку вдоль.")]
        public bool flipScreenV;
        [Tooltip("Образец строки показаний: шрифт, кегль, цвет. Пусто — показаний не будет.")]
        public TextMeshProUGUI readoutPrefab;
        [Tooltip("В каком углу стекла стоят показания.")]
        public Corner readoutCorner = Corner.TopLeft;
        [Tooltip("Отступ от края стекла, в пикселях текстуры.")]
        public Vector2 readoutMargin = new(16f, 16f);
        [Tooltip("Образец подписи имени на краю экрана. Пусто — имён не будет.")]
        public TextMeshPro railLabelPrefab;
        [Tooltip("Образец выноски от подписи к объекту.")]
        public LineRenderer railLeaderPrefab;

        /// <summary>Готовая строка показаний. Создаётся при запуске из образца.</summary>
        public TextMeshProUGUI readout { get; private set; }

        public RenderTexture texture { get; private set; }
        /// <summary>
        /// Точка, на которой стоит начало сцены: вокруг неё вращается вид. Не обязательно тело —
        /// на точку манёвра смотрят не реже, чем на луны.
        /// </summary>
        public SimTransform focus;

        float yaw = 0f;
        float pitch = 60f;
        int focusIndex = -1;

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
        /// <summary>Пиксели текстуры в доли её высоты — в этой мере считается вся геометрия прибора.</summary>
        public double Fraction(float pixels) => pixels / (double)Mathf.Max(1, textureHeight);
        public double MarkerSceneDiameter => NavScale.MarkerSceneDiameter(Fraction(markerPixels), NavScale.OrthographicSize);
        public double LabelSceneHeight => NavScale.MarkerSceneDiameter(Fraction(labelPixels), NavScale.OrthographicSize);
        public float LineSceneWidth => (float)NavScale.MarkerSceneDiameter(Fraction(linePixels), NavScale.OrthographicSize);

        public Color ManeuverColor(int sequenceIndex)
        {
            switch (sequenceIndex)
            {
                case 0: return maneuver1Color;
                case 1: return maneuver2Color;
                default: return maneuver3Color;
            }
        }

        void Awake()
        {
            instance = this;
            if (rangeStep < 0) rangeStep = StepAt(DefaultRange);
            range = TargetRange;
            SimView.metersPerSceneUnit = NavScale.MetersPerSceneUnit(range);

            if (normalizeScreenUV) ScreenGlass.NormalizeUV(surface, screenRotation, flipScreenU, flipScreenV);
            textureWidth = ScreenGlass.TextureWidth(surface, textureHeight, textureWidth);
            texture = new RenderTexture(textureWidth, textureHeight, 24) { name = "NavDisplay" };
            CreateCamera();
            CreateReadout();
            CreateLabelRail();
            ScreenGlass.Show(surface, texture);
        }

        void OnDestroy()
        {
            foreach (GameObject obj in made)
            {
                if (obj != null) Destroy(obj);
            }
            made.Clear();
            if (texture == null) return;
            // Текстура — не объект сцены, её никто не соберёт: RenderTexture держит память
            // на видеокарте, пока её явно не отпустят.
            texture.Release();
            Destroy(texture);
        }

        /// <summary>
        /// Объект, который прибор делает себе сам. Дочерний — чтобы не сорить в корне сцены
        /// и умереть вместе с прибором.
        ///
        /// Масштаб родителя гасится намеренно. NavDisplayPanel живёт на RectTransform внутри
        /// канваса, и единичный масштаб там не гарантирован, а от масштаба этих объектов
        /// зависит размер подписей и показаний. Унаследованное растяжение проявилось бы как
        /// «текст почему-то не того размера» — симптом, по которому причину не найти.
        /// </summary>
        GameObject Child(string name, int layer)
        {
            GameObject child = new(name) { layer = layer };
            child.transform.SetParent(transform, false);
            Vector3 parent = transform.lossyScale;
            child.transform.localScale = new Vector3(
                parent.x == 0f ? 1f : 1f / parent.x,
                parent.y == 0f ? 1f : 1f / parent.y,
                parent.z == 0f ? 1f : 1f / parent.z);
            made.Add(child);
            return child;
        }

        /// <summary>
        /// Камера прибора создаётся при запуске, а не ставится в сцене: двигать её нельзя —
        /// положение и поворот прибор ведёт сам каждый кадр, — а её настройки не дело вкуса,
        /// а условия, при которых верны все расчёты масштаба.
        ///
        /// Ортографическая проекция: в перспективе две одинаковые метки на разной глубине
        /// имели бы разный экранный размер, и промежуточный размер снова начал бы что-то
        /// означать. Размер вида связан с NavScale, от которого считается всё остальное.
        /// Видит камера только слой симуляции: на стекле прибора не может оказаться ничего,
        /// кроме показаний прибора. Это правило игры, а не оптимизация.
        ///
        /// </summary>
        void CreateCamera()
        {
            GameObject camObject = Child("NavCamera", LayerMask.NameToLayer("Simulation"));

            cam = camObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = (float)NavScale.OrthographicSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = screenBackground;
            cam.cullingMask = 1 << camObject.layer;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 200f;
            cam.targetTexture = texture;
        }

        /// <summary>
        /// Показания — часть картинки прибора, а не наклейка на стекле: у настоящего
        /// индикатора изображение одно. Поэтому холст висит перед камерой прибора и попадает
        /// в ту же текстуру, что и обстановка.
        ///
        /// Создаётся кодом, и это не отступление от правила «сцену собирают руками», а его
        /// следствие: место показаний не выбирают глазами. Оно выводится из камеры — ровно
        /// перед ней, с масштабом, при котором высота холста равна высоте текстуры. Только
        /// тогда «кегль 18» означает 18 пикселей из 768, а не случайную долю кадра.
        ///
        /// Как показания выглядят, код не решает: он клонирует ваш образец.
        /// </summary>
        void CreateReadout()
        {
            if (readoutPrefab == null)
            {
                Debug.LogWarning($"NavDisplayPanel на «{name}»: не задан образец строки показаний — " +
                    "чисел на приборе не будет.", this);
                return;
            }

            GameObject canvasObject = new("NavReadout");
            canvasObject.layer = cam.gameObject.layer;
            canvasObject.transform.SetParent(cam.transform, false);

            made.Add(canvasObject);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;

            RectTransform rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(textureWidth, textureHeight);
            rect.localPosition = new Vector3(0f, 0f, 1f);
            rect.localRotation = Quaternion.identity;
            float scale = (float)(2.0 * NavScale.OrthographicSize) / textureHeight;
            rect.localScale = new Vector3(scale, scale, scale);

            readout = Instantiate(readoutPrefab, canvasObject.transform);
            readout.gameObject.name = "Readout";
            readout.gameObject.layer = canvasObject.layer;
            readout.raycastTarget = false;
            readout.enabled = true;
            readout.gameObject.SetActive(true);
            readout.fontSize = Mathf.Max(readoutPixels, 1f);
            PinToCorner(readout.rectTransform);
            Debug.Log($"NavDisplayPanel: показания созданы. Слой {readout.gameObject.layer} " +
                $"(камера видит маску {cam.cullingMask}), шрифт " +
                $"{(readout.font == null ? "НЕТ" : readout.font.name)}, кегль {readout.fontSize}, " +
                $"цвет {readout.color}, прямоугольник {readout.rectTransform.rect.size} " +
                $"в точке {readout.rectTransform.anchoredPosition}, масштаб холста " +
                $"{canvasObject.transform.lossyScale}, текст «{readout.text}».", readout);
            // Линии прибора рисуются в очереди Overlay и легли бы поверх строк. Показания —
            // единственное, что не имеет права быть перечёркнутым. В коде, а не в материале:
            // материал шрифта общий, и правка в нём разъехалась бы по всем надписям проекта.
            readout.fontMaterial.renderQueue = 4100;
        }

        /// <summary>
        /// Рельса подписей — тоже часть картинки прибора: имена рисуются той же камерой и
        /// попадают в ту же текстуру. Где стоит её объект, не значит ничего — она раскладывает
        /// подписи по кадру камеры, в мировых координатах. Ставить такое в сцену незачем:
        /// двигать нечего, а ошибиться слоем — запросто.
        ///
        /// Как подписи выглядят, решает ваш образец.
        /// </summary>
        void CreateLabelRail()
        {
            if (railLabelPrefab == null || railLeaderPrefab == null)
            {
                Debug.LogWarning($"NavDisplayPanel на «{name}»: не заданы образцы подписи " +
                    "и выноски — имён объектов на приборе не будет.", this);
                return;
            }

            GameObject railObject = Child("NavLabelRail", cam.gameObject.layer);

            NavLabelRail rail = railObject.AddComponent<NavLabelRail>();
            rail.labelPrefab = railLabelPrefab;
            rail.leaderPrefab = railLeaderPrefab;
        }

        /// <summary>
        /// Показания прижимаются к углу стекла отступом в пикселях текстуры. Якоря образца
        /// при этом переписываются, и намеренно: образец не знает и не должен знать размер
        /// холста прибора. Позиция, осмысленная на холсте 1920 × 1080, на холсте 1024 × 768
        /// уезжает за край — так и случилось. Размер блока остаётся вашим: его задаёт
        /// Size Delta образца.
        /// </summary>
        void PinToCorner(RectTransform rect)
        {
            bool left = readoutCorner is Corner.TopLeft or Corner.BottomLeft;
            bool top = readoutCorner is Corner.TopLeft or Corner.TopRight;

            Vector2 corner = new(left ? 0f : 1f, top ? 1f : 0f);
            rect.anchorMin = corner;
            rect.anchorMax = corner;
            rect.pivot = corner;
            rect.anchoredPosition = new Vector2(
                left ? readoutMargin.x : -readoutMargin.x,
                top ? -readoutMargin.y : readoutMargin.y);
        }

        void UpdateReadout()
        {
            if (readout == null) return;
            Ship ship = SimMono.playerShip as Ship;
            if (ship == null) return;
            readout.text = BuildReadout(ship, GameMono.instance.Epoch);
        }

        static string Km(double meters) => $"{meters / 1000.0:N0} km";

        /// <summary>
        /// Показания рамкой: цель сверху, план снизу таблицей — по строке на узел и сумма под
        /// ней. Ширина рамки считается от самой длинной строки внутри, чтобы длинное имя цели
        /// не вылезало за края таблицы манёвров — разница уходит в столбец времени, он и так
        /// текст переменной длины.
        /// </summary>
        static string BuildReadout(Ship ship, double epoch)
        {
            SpaceObject target = SimMono.target;
            const string targetTitle = "TARGET";
            string targetLine = target == null ? "NONE" : $"{target.GameObject.name}   R {Km(target.radius)}";

            List<Maneuver> maneuvers = ship.Maneuvers();
            string[] header = { "M", "DV m/s", "T" };
            List<string[]> rows = new();
            double plannedTotal = 0.0;
            foreach (Maneuver maneuver in maneuvers)
            {
                plannedTotal += maneuver.PlannedMagnitude;
                rows.Add(new[]
                {
                    (rows.Count + 1).ToString(),
                    maneuver.PlannedMagnitude.ToString("F1"),
                    TrajectoryRenderer.Countdown(maneuver.startEpoch - epoch),
                });
            }
            string totalLine = $"TOTAL   {plannedTotal:F1} m/s";

            int[] colWidth = new int[header.Length];
            for (int c = 0; c < header.Length; c++) colWidth[c] = header[c].Length;
            foreach (string[] row in rows)
                for (int c = 0; c < row.Length; c++) colWidth[c] = Mathf.Max(colWidth[c], row[c].Length);
            string columnsBorder = AsciiTable.ColumnsBorder(colWidth, AsciiTable.TopJoint);

            int lineLength = Mathf.Max(columnsBorder.Length, targetLine.Length + 4);
            lineLength = Mathf.Max(lineLength, targetTitle.Length + 2);
            if (maneuvers.Count > 0) lineLength = Mathf.Max(lineLength, totalLine.Length + 4);
            if (lineLength > columnsBorder.Length) colWidth[^1] += lineLength - columnsBorder.Length;

            StringBuilder text = new(AsciiTable.TitledBorder(targetTitle, lineLength));
            text.Append('\n').Append(AsciiTable.Row(targetLine, lineLength));
            if (maneuvers.Count > 0)
            {
                text.Append('\n').Append(AsciiTable.ColumnsBorder(colWidth, AsciiTable.TopJoint));
                text.Append('\n').Append(AsciiTable.ColumnsRow(header, colWidth));
                text.Append('\n').Append(AsciiTable.ColumnsBorder(colWidth, AsciiTable.Cross));
                foreach (string[] row in rows) text.Append('\n').Append(AsciiTable.ColumnsRow(row, colWidth));
                text.Append('\n').Append(AsciiTable.ColumnsBorder(colWidth, AsciiTable.BottomJoint));
                text.Append('\n').Append(AsciiTable.Row(totalLine, lineLength));
            }
            text.Append('\n').Append(AsciiTable.Border(lineLength, AsciiTable.BottomLeft, AsciiTable.BottomRight));
            return text.ToString();
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

        /// <summary>Перевести центр навигационного экрана на тело или точку манёвра.</summary>
        public void FocusOn(SimTransform point)
        {
            if (point == null) return;
            focus = point;
            focusIndex = FocusPoints().IndexOf(focus);
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
            int maneuverStart = points.Count;
            for (Maneuver maneuver = (SimMono.playerShip as Ship)?.GetManeuver();
                 maneuver != null;
                 maneuver = maneuver.Previous)
            {
                // Цепочка хранится от последнего узла назад. Вставка в одну позицию
                // разворачивает её в порядок исполнения: MANEUVER 1, 2, 3.
                points.Insert(maneuverStart, maneuver.simTransform);
            }
            return points;
        }
    }
}
