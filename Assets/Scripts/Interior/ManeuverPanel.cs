using System.Collections.Generic;
using System.Text;
using Game;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using TMPro;
using UnityEngine;
using Utilities;

namespace Interior
{
    /// <summary>
    /// Текстовый экран манёвра. Сам создаёт камеру, холст и текстуру для стекла,
    /// а также обновляет показания корабля.
    /// </summary>
    public class ManeuverPanel : MonoBehaviour
    {
        const string ScreenLayer = "Panels";

        [Tooltip("Поверхность экрана: меш с UV-развёрткой.")]
        public Renderer surface;
        [Tooltip("Разрешение экрана по высоте. Ширина берётся из пропорций стекла.")]
        public int textureHeight = 360;
        [Tooltip("Запасная ширина, если пропорции стекла не удалось определить.")]
        public int textureWidth = 256;
        public Color screenBackground = Color.black;
        public bool normalizeScreenUV = true;
        public ScreenTurn screenRotation = ScreenTurn.Deg0;
        public bool flipScreenU;
        public bool flipScreenV;

        [Tooltip("Размер шрифта в пикселях текстуры. Кегль образца заменяется этим значением.")]
        public float labelPixels = 18f;
        [Tooltip("Отступ текста от верхнего левого угла, в пикселях текстуры.")]
        public Vector2 margin = new(12f, 12f);
        [Tooltip("Интервал обновления показаний в кадрах.")]
        public int everyFrames = 10;
        [Tooltip("Ширина рамки в знаках. Ноль меряет шаг знака у шрифта и растягивает рамку по стеклу.")]
        public int lineColumns;

        GameObject rig;
        RenderTexture texture;
        TextMeshProUGUI readout;
        /// <summary>Ширина стекла в знаках: рамка растягивается на неё, а не на длину текста.</summary>
        int columns;

        void Awake()
        {
            if (surface == null || NavPalette.ReadoutPrefab == null)
            {
                Debug.LogError($"ManeuverPanel на «{name}»: нет стекла или в NavPalette не задан " +
                    "образец строки показаний.", this);
                enabled = false;
                return;
            }

            int layer = LayerMask.NameToLayer(ScreenLayer);
            if (layer < 0)
            {
                Debug.LogError($"ManeuverPanel на «{name}»: отсутствует слой «{ScreenLayer}».", this);
                enabled = false;
                return;
            }

            if (normalizeScreenUV) ScreenGlass.NormalizeUV(surface, screenRotation, flipScreenU, flipScreenV);
            textureWidth = ScreenGlass.TextureWidth(surface, textureHeight, textureWidth);
            texture = new RenderTexture(textureWidth, textureHeight, 24) { name = $"Maneuver {name}" };
            Build(layer);
            ScreenGlass.Show(surface, texture);
            Refresh();
        }

        void OnDestroy()
        {
            if (rig != null) Destroy(rig);
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
        }

        void Build(int layer)
        {
            rig = new GameObject($"Maneuver {name}") { layer = layer };
            rig.transform.position = ScreenGlass.NextRigPosition();

            Camera cam = rig.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.5f * textureHeight;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = screenBackground;
            cam.cullingMask = 1 << layer;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 2f * textureHeight;
            cam.targetTexture = texture;

            GameObject canvasObject = new("Canvas") { layer = layer };
            canvasObject.transform.SetParent(rig.transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(textureWidth, textureHeight);
            canvasRect.localPosition = new Vector3(0f, 0f, 1f);

            // Образец общий для всех табло кабины: шрифт и его материал. Кегль, цвет и
            // раскладка ниже — свои, они в пикселях этой текстуры.
            readout = Instantiate(NavPalette.ReadoutPrefab, canvasObject.transform);
            readout.gameObject.name = "Readout";
            readout.gameObject.layer = layer;
            readout.raycastTarget = false;
            readout.fontSize = Mathf.Max(labelPixels, 1f);

            RectTransform textRect = readout.rectTransform;
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(0f, 1f);
            textRect.pivot = new Vector2(0f, 1f);
            textRect.anchoredPosition = new Vector2(margin.x, -margin.y);
            textRect.sizeDelta = new Vector2(
                Mathf.Max(1f, textureWidth - 2f * margin.x),
                Mathf.Max(1f, textureHeight - 2f * margin.y));
            // Заданное руками число знаков важнее меренного: у экрана может быть своя
            // причина быть уже стекла — рамка соседней панели, наклейка, вырез.
            columns = lineColumns > 0 ? lineColumns : NavText.Columns(readout, textRect.sizeDelta.x);
        }

        void Update()
        {
            if (Time.frameCount % Mathf.Max(everyFrames, 1) == 0) Refresh();
        }

        const string Title = "MANEUVER";

        void Refresh()
        {
            Ship ship = SimMono.playerShip as Ship;
            Maneuver maneuver = ship?.GetNextManeuver();
            if (maneuver == null)
            {
                int empty = Mathf.Max("NO DATA".Length + 4, Title.Length + 2);
                empty = Mathf.Max(empty, columns);
                readout.text = NavText.Frame(
                    NavText.Paint(AsciiTable.TitledBorder(Title, empty), NavText.Label) + "\n" +
                    NavText.Paint(AsciiTable.Row("NO DATA", empty), NavText.Label) + "\n" +
                    NavText.Paint(AsciiTable.Border(empty, AsciiTable.BottomLeft, AsciiTable.BottomRight), NavText.Label));
                return;
            }

            double epoch = GameMono.instance.Epoch;
            string burnLine = $"BURN {TrajectoryRenderer.Countdown(ship.BurnStartEpoch - epoch)}";
            string nodeLine = $"NODE {TrajectoryRenderer.Countdown(maneuver.startEpoch - epoch)}";
            string dvLine = $"DV {maneuver.PlannedMagnitude - ship.BurnedDeltaV:F1}/{maneuver.PlannedMagnitude:F1} m/s";

            (double curPe, double curAp) = AstroDynamic.GetPeriapsisAndApoapsis(ship.orbitParams);
            (double tgtPe, double tgtAp) = AstroDynamic.GetPeriapsisAndApoapsis(maneuver.newOrbitParams);
            string[] header = { "", "CURRENT", "TARGET" };
            List<string[]> rows = new()
            {
                new[] { "PE", Km(curPe), Km(tgtPe) },
                new[] { "AP", Km(curAp), Km(tgtAp) },
                new[] { "E", $"{ship.orbitParams.eccentricity:F4}", $"{maneuver.newOrbitParams.eccentricity:F4}" },
                new[] { "I", $"{ship.orbitParams.inclination:F2}°", $"{maneuver.newOrbitParams.inclination:F2}°" },
            };

            int[] colWidth = new int[header.Length];
            for (int c = 0; c < header.Length; c++) colWidth[c] = header[c].Length;
            foreach (string[] row in rows)
                for (int c = 0; c < row.Length; c++) colWidth[c] = Mathf.Max(colWidth[c], row[c].Length);
            string columnsBorder = AsciiTable.ColumnsBorder(colWidth, AsciiTable.TopJoint);

            int lineLength = columnsBorder.Length;
            lineLength = Mathf.Max(lineLength, burnLine.Length + 4);
            lineLength = Mathf.Max(lineLength, nodeLine.Length + 4);
            lineLength = Mathf.Max(lineLength, dvLine.Length + 4);
            lineLength = Mathf.Max(lineLength, Title.Length + 2);
            // Рамка по ширине стекла: таблица в половину экрана читается как обрывок, а
            // лишнюю ширину есть куда деть — в столбец, который и так переменной длины.
            lineLength = Mathf.Max(lineLength, columns);
            if (lineLength > columnsBorder.Length) colWidth[^1] += lineLength - columnsBorder.Length;

            // Столбец «что сейчас» — своей ролью, «что получится» — цветом той траектории,
            // которую этот узел рисует на навигационном экране: панель и картинка говорят
            // про один и тот же манёвр и обязаны называть его одинаково.
            Color label = NavText.Label;
            Color planned = NavPalette.Maneuver(maneuver.SequenceIndex);

            StringBuilder text = new(NavText.Paint(AsciiTable.TitledBorder(Title, lineLength), label));
            text.Append('\n').Append(NavText.Paint(AsciiTable.Row(burnLine, lineLength), NavPalette.Own));
            text.Append('\n').Append(NavText.Paint(AsciiTable.Row(nodeLine, lineLength), NavPalette.Own));
            text.Append('\n').Append(NavText.Paint(AsciiTable.Row(dvLine, lineLength), NavPalette.Own));
            text.Append('\n').Append(NavText.Paint(
                AsciiTable.TitledColumnsBorder(header, colWidth, AsciiTable.TopJoint), label));
            foreach (string[] row in rows)
            {
                string[] painted =
                {
                    NavText.Cell(row[0], colWidth[0], label),
                    NavText.Cell(row[1], colWidth[1], NavPalette.Own),
                    NavText.Cell(row[2], colWidth[2], planned),
                };
                text.Append('\n').Append(AsciiTable.ColumnsRow(painted, colWidth));
            }
            text.Append('\n').Append(NavText.Paint(
                AsciiTable.ColumnsBorder(colWidth, AsciiTable.BottomJoint), label));
            text.Append('\n').Append(NavText.Paint(
                AsciiTable.Border(lineLength, AsciiTable.BottomLeft, AsciiTable.BottomRight), label));
            readout.text = NavText.Frame(text.ToString());
        }

        static string Km(double meters) => $"{meters / 1000.0:N0} km";
    }
}
