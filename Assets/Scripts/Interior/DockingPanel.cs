using System.Globalization;
using DoublePrecision;
using OuterSpace;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using TMPro;
using UnityEngine;
using Utilities;

namespace Interior
{
    /// <summary>
    /// Экран стыковки: картинка с камеры в активном узле корабля и поверх неё прицел и
    /// показания узла относительно узла станции-цели. Прицел неподвижен — это ось узла
    /// корабля; совмещают с ним узел станции на картинке, а числа говорят, насколько
    /// точно.
    ///
    /// Станция с километров меньше пикселя, поэтому узел станции на картинке обведён
    /// рамкой, а вне кадра у края стоит стрелка — куда доворачивать.
    ///
    /// Показаний два набора. Вне маяка оси узла ничего не значат — проекция на ось может
    /// быть нулём в километре сбоку, — поэтому там расстояние, его скорость и угол от оси.
    /// В конусе маяка — положение в осях узла и рассогласование.
    ///
    /// Из сцены нужны меш стекла и вид за бортом, в котором стоит камера: камеру ставит и
    /// ведёт ExteriorView, потому что только он знает, где корабль в сцене ближнего плана.
    ///
    /// Узлы по бортам, и камера смотрит вбок: «вперёд» корабля на картинке — это вправо или
    /// влево, смотря по узлу. Поэтому в левом нижнем углу — оси РСУ, как они лежат в кадре, и
    /// стрелка, куда сейчас толкают сопла.
    /// </summary>
    public class DockingPanel : MonoBehaviour
    {
        const string ScreenLayer = "Panels";

        [Tooltip("Вид за бортом: в его сцене ближнего плана стоит камера стыковки.")]
        public ExteriorView exterior;
        [Tooltip("Разрешение изображения прибора по высоте. Ширина подгоняется под стекло.")]
        public int textureHeight = 512;

        [Tooltip("Угол зрения камеры стыковки по вертикали, градусы.")]
        public float fieldOfView = 30f;
        [Tooltip("Толщина линий в пикселях текстуры.")]
        public float linePixels = 2f;
        [Tooltip("Размах прицела в пикселях текстуры.")]
        public float crossPixels = 80f;
        [Tooltip("Высота строки показаний в пикселях текстуры. Кегль образца на размер не влияет — он нормализуется.")]
        public float labelPixels = 18f;
        [Tooltip("Сторона рамки вокруг узла станции в пикселях текстуры.")]
        public float boxPixels = 28f;
        [Tooltip("Размер стрелки у края, когда узел станции вне кадра, в пикселях текстуры.")]
        public float arrowPixels = 20f;
        [Tooltip("Длина оси РСУ в левом нижнем углу в пикселях текстуры.")]
        public float axisPixels = 40f;

        GameObject rig;
        RenderTexture texture;
        Material lineMaterial;
        int laidOutWidth;
        TextMeshPro readout;
        Camera dockingCamera;
        LineRenderer box;
        LineRenderer arrow;
        Vector3 axesOrigin;
        readonly LineRenderer[] axes = new LineRenderer[3];
        readonly TextMeshPro[] axisLabels = new TextMeshPro[3];
        LineRenderer thrust;

        // Связанные оси корабля в осях сцены ближнего плана (ExteriorView.ToHull): вперёд — Z, вправо — X, вверх — Y.
        static readonly Vector3[] HullAxes = { Vector3.forward, Vector3.right, Vector3.up };
        static readonly string[] AxisNames = { "F", "R", "U" };

        static Ship Ship => SimMono.playerShip as Ship;

        void Awake()
        {
            int layer = LayerMask.NameToLayer(ScreenLayer);
            if (exterior == null || layer < 0)
            {
                Debug.LogError($"DockingPanel на «{name}»: нужны вид за бортом и слой «{ScreenLayer}».", this);
                enabled = false;
                return;
            }
            texture = new RenderTexture(textureHeight, textureHeight, 24) { name = $"Docking {name}" };
            Build(layer);
            ScreenRouter.RegisterFeed(ScreenContent.Docking, texture);
        }

        // Камера заводится в Start: сцену ближнего плана ExteriorView строит в своём Awake.
        void Start()
        {
            if (!exterior.isActiveAndEnabled)
            {
                Debug.LogError($"DockingPanel на «{name}»: вид за бортом выключен — камере стыковки негде стоять.", this);
                enabled = false;
                return;
            }
            // Прицел и показания дорисовываются поверх той же текстуры, поэтому картинка
            // камеры обязана быть готова раньше.
            dockingCamera = exterior.CreateDockingCamera(texture, fieldOfView);
            dockingCamera.depth = rig.GetComponentInChildren<Camera>().depth - 1f;
        }

        void OnDestroy()
        {
            ScreenRouter.UnregisterFeed(ScreenContent.Docking, texture);
            if (rig != null) Destroy(rig);
            if (lineMaterial != null) Destroy(lineMaterial);
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
        }

        /// <summary>Мировая единица прибора равна пикселю текстуры, как у остальных экранов.</summary>
        void Build(int layer)
        {
            lineMaterial = new Material(SimLine.Material) { name = "DockingLine", renderQueue = 2900 };
            rig = new GameObject($"Docking {name}") { layer = layer };
            rig.transform.position = ScreenGlass.NextRigPosition();

            GameObject eye = new("Camera") { layer = layer };
            eye.transform.SetParent(rig.transform, false);
            eye.transform.localPosition = new Vector3(0f, 0f, -2f * textureHeight);
            Camera cam = eye.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.5f * textureHeight;
            cam.clearFlags = CameraClearFlags.Depth;
            cam.cullingMask = 1 << layer;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 4f * textureHeight;
            cam.targetTexture = texture;

            float arm = 0.5f * crossPixels;
            float gap = 0.25f * arm;
            Segment(layer, new Vector3(-arm, 0f), new Vector3(-gap, 0f));
            Segment(layer, new Vector3(gap, 0f), new Vector3(arm, 0f));
            Segment(layer, new Vector3(0f, -arm), new Vector3(0f, -gap));
            Segment(layer, new Vector3(0f, gap), new Vector3(0f, arm));

            float half = 0.5f * boxPixels;
            box = Line(layer, "Box", NavPalette.Target);
            box.loop = true;
            box.positionCount = 4;
            box.SetPositions(new[] { new Vector3(-half, -half), new Vector3(half, -half), new Vector3(half, half), new Vector3(-half, half) });
            // Стрелка смотрит вдоль своей оси X; куда — задаёт поворот.
            arrow = Line(layer, "Arrow", NavPalette.Target);
            arrow.positionCount = 3;
            arrow.SetPositions(new[] { new Vector3(-arrowPixels, 0.6f * arrowPixels), Vector3.zero, new Vector3(-arrowPixels, -0.6f * arrowPixels) });

            for (int i = 0; i < 3; i++)
            {
                axes[i] = Line(layer, $"Axis {AxisNames[i]}", NavPalette.Other);
                axes[i].positionCount = 2;
            }
            thrust = Line(layer, "Thrust", NavPalette.Target);
            thrust.positionCount = 2;

            if (NavPalette.LabelPrefab == null) return;
            readout = Label(layer, "Readout", TextAlignmentOptions.TopLeft, new Vector2(0f, 1f));
            for (int i = 0; i < 3; i++)
            {
                axisLabels[i] = Label(layer, $"Axis {AxisNames[i]}", TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f));
                axisLabels[i].color = NavPalette.Other;
            }
        }

        /// <summary>Раскладка по углам: ширину текстуры задаёт стекло, и она может смениться.</summary>
        void Layout()
        {
            laidOutWidth = texture.width;
            float margin = 0.5f * labelPixels;
            axesOrigin = new Vector3(-0.5f * texture.width + margin + axisPixels + labelPixels, -0.5f * textureHeight + margin + axisPixels + labelPixels, 0f);
            if (readout != null) readout.transform.localPosition = new Vector3(-0.5f * texture.width + margin, 0.5f * textureHeight - margin, 0f);
        }

        TextMeshPro Label(int layer, string name, TextAlignmentOptions alignment, Vector2 pivot)
        {
            TextMeshPro label = Instantiate(NavPalette.LabelPrefab, rig.transform);
            label.gameObject.name = name;
            label.gameObject.layer = layer;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.color = NavPalette.Own;
            // Точка отсчёта — с той же стороны, что и выравнивание: иначе строки уезжают за край.
            label.alignment = alignment;
            label.rectTransform.pivot = pivot;
            label.text = "Xg";
            label.ForceMeshUpdate();
            if (label.preferredHeight > 0f) label.transform.localScale = Vector3.one * (labelPixels / label.preferredHeight);
            return label;
        }

        void Segment(int layer, Vector3 from, Vector3 to)
        {
            LineRenderer line = Line(layer, "Cross", NavPalette.Own);
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
        }

        LineRenderer Line(int layer, string name, Color color)
        {
            GameObject host = new(name) { layer = layer };
            host.transform.SetParent(rig.transform, false);
            LineRenderer line = host.AddComponent<LineRenderer>();
            line.material = lineMaterial;
            line.useWorldSpace = false;
            line.widthMultiplier = linePixels;
            line.startColor = color;
            line.endColor = color;
            return line;
        }

        void LateUpdate()
        {
            Ship ship = Ship;
            if (texture.width != laidOutWidth) Layout();
            if (ship == null) return;
            DrawMarker(ship);
            DrawAxes(ship);
            if (readout != null) readout.text = Readout(ship);
        }

        /// <summary>
        /// Узел станции в кадре — рамка вокруг него; вне кадра или за спиной — стрелка у края
        /// в ту сторону, куда доворачивать.
        /// </summary>
        void DrawMarker(Ship ship)
        {
            bool show = dockingCamera != null && ship.DockedTo == null && SimMono.target is Station && ship.TargetPort != null;
            box.gameObject.SetActive(false);
            arrow.gameObject.SetActive(false);
            if (!show) return;

            Station station = (Station)SimMono.target;
            Vector3 view = dockingCamera.WorldToViewportPoint(exterior.ProximityPoint(station.simTransform.GLOBAL_R + ship.TargetPort.position));
            Vector2 point = new((view.x - 0.5f) * texture.width, (view.y - 0.5f) * textureHeight);
            float halfWidth = 0.5f * texture.width - arrowPixels;
            float halfHeight = 0.5f * textureHeight - arrowPixels;
            if (view.z > 0f && Mathf.Abs(point.x) <= halfWidth && Mathf.Abs(point.y) <= halfHeight)
            {
                box.gameObject.SetActive(true);
                box.transform.localPosition = point;
                return;
            }
            // За спиной проекция зеркальна: доворачивать надо в противоположную сторону.
            Vector2 direction = view.z > 0f ? point : -point;
            if (direction.sqrMagnitude < 1e-6f) direction = Vector2.up;
            float scale = Mathf.Min(halfWidth / Mathf.Max(Mathf.Abs(direction.x), 1e-6f), halfHeight / Mathf.Max(Mathf.Abs(direction.y), 1e-6f));
            arrow.gameObject.SetActive(true);
            arrow.transform.localPosition = direction * scale;
            arrow.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        /// <summary>
        /// Оси проецируются в кадр камеры узла; ось вдоль взгляда вырождается в точку, и тогда
        /// подпись говорит, от экрана она или на экран. Стрелка — сумма команд по осям.
        /// </summary>
        void DrawAxes(Ship ship)
        {
            bool show = dockingCamera != null;
            thrust.gameObject.SetActive(false);
            for (int i = 0; i < 3; i++)
            {
                axes[i].gameObject.SetActive(show);
                if (axisLabels[i] != null) axisLabels[i].gameObject.SetActive(show);
            }
            if (!show) return;

            Quaternion toView = Quaternion.Inverse(dockingCamera.transform.localRotation);
            // Команда РСУ по осям вперёд, влево, вверх; на экране нужна вправо.
            Vector3d command = ship.rcs.command;
            float[] commands = { (float)command.x, (float)-command.y, (float)command.z };
            Vector3 push = Vector3.zero;
            for (int i = 0; i < 3; i++)
            {
                Vector3 view = toView * HullAxes[i];
                Vector3 tip = new Vector3(view.x, view.y) * axisPixels;
                push += tip * commands[i];
                Color color = commands[i] != 0f ? NavPalette.Target : NavPalette.Other;
                axes[i].startColor = color;
                axes[i].endColor = color;
                axes[i].SetPosition(0, axesOrigin);
                axes[i].SetPosition(1, axesOrigin + tip);
                if (axisLabels[i] == null) continue;
                bool alongView = tip.magnitude < 0.3f * axisPixels;
                axisLabels[i].color = color;
                axisLabels[i].text = !alongView ? AxisNames[i] : AxisNames[i] + (view.z > 0f ? " IN" : " OUT");
                Vector3 offset = alongView ? new Vector3(0f, -0.8f * labelPixels) : tip.normalized * (0.7f * labelPixels);
                axisLabels[i].transform.localPosition = axesOrigin + tip + offset;
            }
            if (push.sqrMagnitude < 1e-6f) return;
            thrust.gameObject.SetActive(true);
            thrust.SetPosition(0, axesOrigin);
            thrust.SetPosition(1, axesOrigin + push);
        }

        static string Readout(Ship ship)
        {
            string port = ship.rightPortActive ? "PORT R" : "PORT L";
            if (ship.Docking is not DockingState d) return $"{port}\nNO TARGET PORT";
            string beacon = ship.Latched ? "DOCKED" : ship.DockedTo != null ? "DOCKING READY" :
                ship.BeaconHolding ? "BCN HOLD" : ship.BeaconLocked ? "BCN LOCK" : "BCN ----";
            string head = string.Format(CultureInfo.InvariantCulture,
                "{0}   {1}\n" +
                "DIST {2,9}   RATE {3,7:+0.00;-0.00} m/s\n" +
                "REL  {4,7:F2} m/s   OFF AXIS {5,5:F1}",
                port, beacon, Distance(d.Distance), d.RangeRate, d.Speed, d.OffAxis);
            if (!ship.BeaconLocked && !ship.BeaconHolding) return head;
            return head + string.Format(CultureInfo.InvariantCulture,
                "\n" +
                "RNG  {0,8:F1} m   CLS {1,6:+0.00;-0.00} m/s\n" +
                "SIDE {2,8:+0.00;-0.00} m   {3,10:+0.00;-0.00} m/s\n" +
                "UP   {4,8:+0.00;-0.00} m   {5,10:+0.00;-0.00} m/s\n" +
                "ROLL {6,5:+0.0;-0.0}  PIT {7,5:+0.0;-0.0}  YAW {8,5:+0.0;-0.0}",
                d.Range, d.ClosingSpeed, d.Side, d.SideSpeed, d.Up, d.UpSpeed, d.Roll, d.Pitch, d.Yaw);
        }

        static string Distance(double metres) => metres >= 10000.0
            ? string.Format(CultureInfo.InvariantCulture, "{0:F1} km", metres / 1000.0)
            : string.Format(CultureInfo.InvariantCulture, "{0:F1} m", metres);
    }
}
