using System.Collections.Generic;
using DoublePrecision;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using TMPro;
using UnityEngine;
using Utilities;

namespace Interior
{
    /// <summary>
    /// Шар-указатель: то же, что авиагоризонт у самолёта, только горизонт здесь местный —
    /// плоскость, перпендикулярная радиус-вектору. Корабль неподвижен в центре, вокруг него
    /// крутится сетка местной системы (LVLH): полюса — зенит и надир, экватор — местный
    /// горизонт, начало отсчёта долготы — програда.
    ///
    /// Плоской лестницы тангажа здесь быть не может: в космосе нос уходит через зенит и
    /// шкала переворачивается, а шар на этом месте просто продолжает вращаться. Та же
    /// причина, по которой на «Союзе» стоит шар, а не авиагоризонт.
    ///
    /// Разметка рисуется линиями, как и всё на приборах корабля: сетка, край шара, метки
    /// направлений. Дальняя половина сетки не рисуется вовсе — иначе меридианы просвечивают
    /// насквозь, и по картинке не понять, куда корабль смотрит.
    ///
    /// Из сцены нужен меш стекла. Шрифт и цвета берутся из NavPalette, общей на все экраны
    /// кабины: шар — такой же прибор, как остальные, и говорить он должен на том же языке.
    /// Где стоит камера и как разложены дуги, решает код: это не вкусовое, а следствие
    /// разрешения текстуры.
    /// </summary>
    public class AttitudePanel : MonoBehaviour
    {
        const string ScreenLayer = "Panels";
        // Шаг сетки в 30°: мельче — рябит на экране в два-три сантиметра, крупнее — нечем
        // мерить угол на глаз.
        const float GridStep = 30f;
        const int PointsPerCircle = 72;
        const int RingPoints = 16;

        /// <summary>Метки направлений. Порядок — порядок отрисовки, важен только при наложении.</summary>
        static readonly (ShipOrientation mode, string label)[] Markers =
        {
            (ShipOrientation.Prograde, "PRO"),
            (ShipOrientation.Retrograde, "RET"),
            (ShipOrientation.Target, "TGT"),
            (ShipOrientation.Maneuver, "MNV"),
        };

        [Tooltip("Поверхность, на которой видна картинка прибора: любой меш с UV.")]
        public Renderer surface;
        [Tooltip("Разрешение прибора по высоте. Ширина считается из пропорций стекла.")]
        public int textureHeight = 256;
        [Tooltip("Ширина текстуры. Подгоняется под меш при запуске; заданное здесь значение — запас на случай, если поверхности нет.")]
        public int textureWidth = 256;
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

        [Tooltip("Диаметр шара в пикселях текстуры.")]
        public float ballPixels = 190f;
        [Tooltip("Толщина линий в пикселях текстуры.")]
        public float linePixels = 2f;
        [Tooltip("Размер индекса корабля — неподвижного креста в центре — в пикселях текстуры.")]
        public float crossPixels = 44f;
        [Tooltip("Высота подписей в пикселях текстуры. Кегль образца на размер не влияет — он нормализуется.")]
        public float labelPixels = 16f;
        [Tooltip("Диаметр кольца метки направления в пикселях текстуры: в него целятся носом.")]
        public float markerPixels = 14f;

        // Цвета — роли из NavPalette, а не поля прибора. Сетка и край шара — обстановка,
        // по которой читают положение; горизонт в ней главная линия, поэтому ступенью выше.
        // Крест в центре — это сам корабль, и светится он в полную силу своей роли.
        static Color GridColor => NavPalette.Other;
        static Color HorizonColor => NavPalette.Dim(NavPalette.Own, NavPalette.NameLevel);
        static Color LimbColor => NavPalette.Dim(NavPalette.Other, NavPalette.LeaderLevel);
        static Color CrossColor => NavPalette.Own;

        /// <summary>
        /// Цвет метки — роль того, на что она показывает: своя скорость своим цветом, цель
        /// цветом цели, узел плана тем же, чем нарисована его дуга на навигационном экране.
        /// Шар и карта обязаны называть одно и то же одинаково, иначе сверять их приходится
        /// по буквам.
        /// </summary>
        static Color MarkerColor(ShipOrientation mode) => mode switch
        {
            ShipOrientation.Target => NavPalette.Target,
            // Шар ведёт к ближайшему узлу, а ближайший — всегда первый в цепочке плана.
            ShipOrientation.Maneuver => NavPalette.Maneuver(0),
            _ => NavPalette.Own,
        };

        GameObject rig;
        RenderTexture texture;
        readonly List<LineRenderer> parallels = new();
        readonly List<LineRenderer> meridians = new();
        readonly List<GameObject> markers = new();
        readonly List<Vector3> ring = new();
        readonly List<Vector3> points = new();
        TextMeshPro readout;
        Material lineMaterial;
        float radius;

        static Ship Ship => SimMono.playerShip as Ship;

        void Awake()
        {
            if (surface == null)
            {
                Debug.LogError($"AttitudePanel на «{name}»: не указано стекло экрана — показывать не на чем.", this);
                enabled = false;
                return;
            }
            int layer = LayerMask.NameToLayer(ScreenLayer);
            if (layer < 0)
            {
                Debug.LogError($"AttitudePanel на «{name}»: в проекте нет слоя «{ScreenLayer}» — " +
                    "камере прибора нечего показывать, кроме чужой обстановки.", this);
                enabled = false;
                return;
            }

            if (normalizeScreenUV) ScreenGlass.NormalizeUV(surface, screenRotation, flipScreenU, flipScreenV);
            textureWidth = ScreenGlass.TextureWidth(surface, textureHeight, textureWidth);
            texture = new RenderTexture(textureWidth, textureHeight, 24) { name = $"Attitude {name}" };
            Build(layer);
            ScreenGlass.Show(surface, texture);
        }

        void OnDestroy()
        {
            if (rig != null) Destroy(rig);
            if (lineMaterial != null) Destroy(lineMaterial);
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
        }

        /// <summary>
        /// Мировая единица прибора равна пикселю его текстуры: камера ортографическая и стоит
        /// ровно на половину высоты. Поэтому «шар 190 пикселей» и «линия 2 пикселя» означают
        /// на экране именно это, и размеры проверяются по скриншоту, а не на глаз.
        /// </summary>
        void Build(int layer)
        {
            radius = 0.5f * ballPixels;
            // Материал линий прибора, но своей копией и очередью пораньше текста: иначе
            // сетка рисовалась бы поверх букв и перечёркивала метки направлений.
            lineMaterial = new Material(SimLine.Material) { name = "AttitudeLine", renderQueue = 2900 };

            rig = new GameObject($"Attitude {name}") { layer = layer };
            rig.transform.position = ScreenGlass.NextRigPosition();

            // Камера — отдельный объект: разметка живёт в начале координат прибора, а камера
            // отодвинута от неё назад. В ортографии расстояние на размер картинки не влияет,
            // только на отсечение.
            GameObject eye = new("Camera") { layer = layer };
            eye.transform.SetParent(rig.transform, false);
            eye.transform.localPosition = new Vector3(0f, 0f, -2f * textureHeight);

            Camera cam = eye.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.5f * textureHeight;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = screenBackground;
            cam.cullingMask = 1 << layer;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 4f * textureHeight;
            cam.targetTexture = texture;

            for (float latitude = -90f + GridStep; latitude < 90f; latitude += GridStep)
            {
                parallels.Add(Line(layer, $"Parallel {latitude:F0}",
                    Mathf.Abs(latitude) < 0.5f ? HorizonColor : GridColor));
            }
            for (float longitude = 0f; longitude < 180f; longitude += GridStep)
            {
                meridians.Add(Line(layer, $"Meridian {longitude:F0}", GridColor));
            }
            Limb(layer);
            Cross(layer);
            Labels(layer);
        }

        /// <summary>
        /// Край шара — единственная линия, которая не движется: в ортографии силуэт сферы это
        /// окружность постоянного радиуса. Без него шар без сетки в кадре читался бы как
        /// пустой экран.
        /// </summary>
        void Limb(int layer)
        {
            LineRenderer line = Line(layer, "Limb", LimbColor);
            line.loop = true;
            line.positionCount = PointsPerCircle;
            for (int i = 0; i < PointsPerCircle; i++)
            {
                float angle = 2f * Mathf.PI * i / PointsPerCircle;
                line.SetPosition(i, new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), 0f));
            }
        }

        /// <summary>
        /// Индекс корабля — то, куда смотрит нос. Неподвижен по определению: прибор показывает
        /// мир относительно корабля, а не наоборот.
        /// </summary>
        void Cross(int layer)
        {
            float arm = 0.5f * crossPixels;
            float gap = 0.25f * arm;
            // Метки направлений лежат на шаре, крест стоит ближе к камере, чтобы не тонуть в них.
            float z = -radius - linePixels;
            Segment(layer, "CrossLeft", new Vector3(-arm, 0f, z), new Vector3(-gap, 0f, z));
            Segment(layer, "CrossRight", new Vector3(gap, 0f, z), new Vector3(arm, 0f, z));
            Segment(layer, "CrossUp", new Vector3(0f, gap, z), new Vector3(0f, arm, z));
        }

        void Segment(int layer, string name, Vector3 from, Vector3 to)
        {
            LineRenderer line = Line(layer, name, CrossColor);
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
        }

        /// <summary>
        /// Метка направления — кольцо с точкой в середине, буквы сбоку. Целятся в точку:
        /// у неё есть середина, у слова «PRO» её нет, и совмещать с индексом носа было бы
        /// нечего. Кольцо остаётся видно и тогда, когда индекс закрыл собой точку.
        ///
        /// Кольцо и точка рисуются в плоскости экрана, а не по шару: метка не лежит на
        /// поверхности, она показывает направление, и разворачиваться вместе с сеткой ей
        /// незачем.
        /// </summary>
        void Labels(int layer)
        {
            foreach ((ShipOrientation mode, string label) in Markers)
            {
                Color color = MarkerColor(mode);
                GameObject holder = new($"Marker {label}") { layer = layer };
                holder.transform.SetParent(rig.transform, false);
                Ring(layer, holder.transform, color);
                Dot(layer, holder.transform, color);
                if (NavPalette.LabelPrefab != null)
                {
                    TextMeshPro text = Label(layer, "Label", holder.transform);
                    text.alignment = TextAlignmentOptions.Center;
                    // Кольцо и буквы — одна метка, и цвет у них один.
                    text.color = color;
                    text.text = label;
                    text.transform.localPosition = new Vector3(
                        0.5f * markerPixels + labelPixels, 0.5f * labelPixels, 0f);
                }
                markers.Add(holder);
            }

            if (NavPalette.LabelPrefab == null) return;
            readout = Label(layer, "Readout", rig.transform);
            readout.alignment = TextAlignmentOptions.Bottom;
            // Тангаж, рыскание и крен — числа, по которым ведут корабль: полная сила роли.
            readout.color = NavPalette.Own;
            readout.transform.localPosition = new Vector3(0f, -0.5f * textureHeight + labelPixels, -radius - linePixels);
        }

        void Ring(int layer, Transform parent, Color color)
        {
            LineRenderer line = Line(layer, "Ring", color, parent);
            line.loop = true;
            line.positionCount = RingPoints;
            float ringRadius = 0.5f * markerPixels;
            for (int i = 0; i < RingPoints; i++)
            {
                float angle = 2f * Mathf.PI * i / RingPoints;
                line.SetPosition(i, new Vector3(ringRadius * Mathf.Cos(angle), ringRadius * Mathf.Sin(angle), 0f));
            }
        }

        /// <summary>
        /// Точка — короткий отрезок с круглыми концами: у LineRenderer нет точки, зато есть
        /// толщина, и кружок нужного размера получается из неё.
        /// </summary>
        void Dot(int layer, Transform parent, Color color)
        {
            LineRenderer line = Line(layer, "Dot", color, parent);
            line.numCapVertices = 4;
            line.widthMultiplier = 0.3f * markerPixels;
            line.positionCount = 2;
            line.SetPosition(0, new Vector3(-0.01f, 0f, 0f));
            line.SetPosition(1, new Vector3(0.01f, 0f, 0f));
        }

        TextMeshPro Label(int layer, string name, Transform parent)
        {
            TextMeshPro text = Instantiate(NavPalette.LabelPrefab, parent);
            text.gameObject.name = name;
            text.gameObject.layer = layer;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            // Кегль образца нормализуется: высоту букв задаёт прибор в пикселях текстуры,
            // иначе одна и та же метка читалась бы по-разному на экранах разного разрешения.
            // fontSize у TextMeshPro — не высота строки в мировых единицах, поэтому высота
            // меряется, а не выводится.
            text.text = "Xg";
            text.ForceMeshUpdate();
            if (text.preferredHeight > 0f) text.transform.localScale = Vector3.one * (labelPixels / text.preferredHeight);
            return text;
        }

        LineRenderer Line(int layer, string name, Color color, Transform parent = null)
        {
            GameObject host = new(name) { layer = layer };
            host.transform.SetParent(parent == null ? rig.transform : parent, false);

            LineRenderer line = host.AddComponent<LineRenderer>();
            line.material = lineMaterial;
            line.useWorldSpace = false;
            line.widthMultiplier = linePixels;
            line.numCapVertices = 0;
            line.startColor = color;
            line.endColor = color;
            line.positionCount = 0;
            return line;
        }

        void LateUpdate()
        {
            Ship ship = Ship;
            if (ship == null || ship.centralBody == null) return;

            Vector3d position = ship.simTransform.RELATIVE_R;
            Vector3d velocity = ship.velocity;
            Vector3d zenith = position.normalized;
            Vector3d normal = Vector3d.Cross(position, velocity).normalized;
            Vector3d prograde = Vector3d.Cross(normal, zenith);

            Attitude attitude = ship.attitude;
            Quaternion grid = Quaternion.LookRotation(ToPanel(attitude, prograde), ToPanel(attitude, zenith));
            DrawGrid(grid);
            DrawMarkers(ship, attitude);
            Readout(ship, attitude, zenith, normal, prograde);
        }

        void DrawGrid(Quaternion grid)
        {
            for (int i = 0; i < parallels.Count; i++)
            {
                float latitude = -90f + GridStep * (i + 1);
                Arc(parallels[i], grid, latitude, true);
            }
            for (int i = 0; i < meridians.Count; i++)
            {
                Arc(meridians[i], grid, GridStep * i, false);
            }
        }

        /// <summary>
        /// Видимая часть окружности на шаре. Дальняя половина отбрасывается: у сферы, снятой
        /// ортографически, пересечение окружности с ближним полупространством — всегда одна
        /// дуга, поэтому одной линии на окружность хватает.
        ///
        /// Дуга собирается от её начала, а не от начала выборки: видимый кусок обычно лежит
        /// на стыке — конец выборки переходит в её начало, — и линия, собранная по порядку
        /// отсчётов, вернулась бы через весь шар хордой.
        ///
        /// Концы доводятся до самого края шара: иначе сетка обрывалась бы, не доходя до
        /// силуэта, на случайную долю шага выборки.
        /// </summary>
        void Arc(LineRenderer line, Quaternion grid, float angle, bool parallel)
        {
            ring.Clear();
            for (int i = 0; i < PointsPerCircle; i++)
            {
                float around = 360f * i / PointsPerCircle;
                ring.Add(grid * (radius * (parallel ? OnSphere(angle, around) : OnSphere(around, angle))));
            }

            points.Clear();
            int first = FirstVisible();
            if (first < 0)
            {
                // Разрыва нет: окружность целиком либо на ближней стороне, либо на дальней.
                if (Visible(ring[0]))
                {
                    points.AddRange(ring);
                    points.Add(ring[0]);
                }
            }
            else
            {
                points.Add(Crossing(ring[Before(first)], ring[first]));
                for (int i = 0; i < PointsPerCircle; i++)
                {
                    Vector3 point = ring[(first + i) % PointsPerCircle];
                    if (!Visible(point))
                    {
                        points.Add(Crossing(points[^1], point));
                        break;
                    }
                    points.Add(point);
                }
            }

            line.positionCount = points.Count;
            if (points.Count > 0) line.SetPositions(points.ToArray());
        }

        /// <summary>Отсчёт, с которого дуга выходит из-за края шара, или −1, если выхода нет.</summary>
        int FirstVisible()
        {
            for (int i = 0; i < ring.Count; i++)
            {
                if (Visible(ring[i]) && !Visible(ring[Before(i)])) return i;
            }
            return -1;
        }

        int Before(int index) => (index + ring.Count - 1) % ring.Count;

        /// <summary>Ближняя к зрителю сторона шара: камера смотрит вдоль +Z.</summary>
        static bool Visible(Vector3 point) => point.z < 0f;

        /// <summary>Точка, где дуга уходит за край шара: там, где отрезок пересекает плоскость взгляда.</summary>
        static Vector3 Crossing(Vector3 from, Vector3 to)
        {
            float span = to.z - from.z;
            return Mathf.Abs(span) < 1e-6f ? from : Vector3.Lerp(from, to, -from.z / span);
        }

        /// <summary>Точка на единичной сфере: широта от экватора-горизонта, долгота от програды.</summary>
        static Vector3 OnSphere(float latitude, float longitude)
        {
            float lat = latitude * Mathf.Deg2Rad;
            float lon = longitude * Mathf.Deg2Rad;
            return new Vector3(
                Mathf.Cos(lat) * Mathf.Sin(lon),
                Mathf.Sin(lat),
                Mathf.Cos(lat) * Mathf.Cos(lon));
        }

        void DrawMarkers(Ship ship, Attitude attitude)
        {
            for (int i = 0; i < markers.Count; i++)
            {
                Vector3d direction = ship.DirectionOf(Markers[i].mode);
                // Нулевого направления не бывает у режима, которому есть на что смотреть:
                // нет цели или манёвра — нет и метки.
                if (direction.sqrMagnitude <= 0.0)
                {
                    markers[i].SetActive(false);
                    continue;
                }
                Vector3 point = ToPanel(attitude, direction);
                // Метка на дальней стороне шара не показывается: она означала бы направление,
                // которого с этого борта не видно, а на плоской картинке легла бы поверх
                // ближней и читалась бы как она.
                markers[i].SetActive(Visible(point));
                markers[i].transform.localPosition = radius * point;
            }
        }

        void Readout(Ship ship, Attitude attitude, Vector3d zenith, Vector3d normal, Vector3d prograde)
        {
            if (readout == null) return;

            Vector3d forward = attitude.Forward;
            double pitch = Mathd.Asin(Mathd.Clamp(Vector3d.Dot(forward, zenith), -1.0, 1.0)) * Mathd.Rad2Deg;
            double yaw = Mathd.Atan2(Vector3d.Dot(forward, normal), Vector3d.Dot(forward, prograde)) * Mathd.Rad2Deg;
            // Крен меряется от местной вертикали: это единственная опора, которая у корабля
            // есть — «верх» в пустоте больше ничем не задан.
            Vector3d upward = (zenith - forward * Vector3d.Dot(forward, zenith)).normalized;
            Vector3d shipUp = attitude.Up;
            double roll = Mathd.Atan2(
                Vector3d.Dot(Vector3d.Cross(upward, shipUp), forward),
                Vector3d.Dot(upward, shipUp)) * Mathd.Rad2Deg;
            double rate = attitude.angularVelocity.magnitude * Mathd.Rad2Deg;

            string content = $"P{pitch,6:+0.0;-0.0} Y{yaw,6:+0.0;-0.0} R{roll,6:+0.0;-0.0} {rate:0.00}/s";
            string title = ship.orientation.ToString().ToUpperInvariant();
            int lineLength = Mathf.Max(content.Length + 4, title.Length + 2);
            // Без нижней рамки: у этого экрана на показания снизу от шара отведено ровно
            // две строки, третья уезжает за верх текстуры камеры прибора.
            readout.text = AsciiTable.TitledBorder(title, lineLength) + "\n" + AsciiTable.Row(content, lineLength);
        }

        /// <summary>
        /// Направление из системы центрального тела в систему прибора. Корабль в приборе
        /// стоит неподвижно: нос смотрит на зрителя, голова пилота — вверх экрана. Правая
        /// система симуляции при этом становится левой системой Unity, как и на
        /// навигационном экране, — это тот же переход, что делает SimView.
        /// </summary>
        static Vector3 ToPanel(Attitude attitude, Vector3d direction) => new(
            (float)Vector3d.Dot(direction, attitude.Left),
            (float)Vector3d.Dot(direction, attitude.Up),
            -(float)Vector3d.Dot(direction, attitude.Forward));
    }
}
