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

        [Tooltip("Образец текста: шрифт и цвет экрана.")]
        public TextMeshProUGUI textPrefab;
        [Tooltip("Размер шрифта в пикселях текстуры. Кегль образца заменяется этим значением.")]
        public float labelPixels = 18f;
        [Tooltip("Отступ текста от верхнего левого угла, в пикселях текстуры.")]
        public Vector2 margin = new(12f, 12f);
        [Tooltip("Интервал обновления показаний в кадрах.")]
        public int everyFrames = 10;

        GameObject rig;
        RenderTexture texture;
        TextMeshProUGUI readout;

        void Awake()
        {
            if (surface == null || textPrefab == null)
            {
                Debug.LogError($"ManeuverPanel на «{name}»: укажите стекло и образец текста.", this);
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

            readout = Instantiate(textPrefab, canvasObject.transform);
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
                readout.text = AsciiTable.TitledBorder(Title, empty) + "\n" +
                    AsciiTable.Row("NO DATA", empty) + "\n" +
                    AsciiTable.Border(empty, AsciiTable.BottomLeft, AsciiTable.BottomRight);
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
            if (lineLength > columnsBorder.Length) colWidth[^1] += lineLength - columnsBorder.Length;

            StringBuilder text = new(AsciiTable.TitledBorder(Title, lineLength));
            text.Append('\n').Append(AsciiTable.Row(burnLine, lineLength));
            text.Append('\n').Append(AsciiTable.Row(nodeLine, lineLength));
            text.Append('\n').Append(AsciiTable.Row(dvLine, lineLength));
            text.Append('\n').Append(AsciiTable.TitledColumnsBorder(header, colWidth, AsciiTable.TopJoint));
            foreach (string[] row in rows) text.Append('\n').Append(AsciiTable.ColumnsRow(row, colWidth));
            text.Append('\n').Append(AsciiTable.ColumnsBorder(colWidth, AsciiTable.BottomJoint));
            text.Append('\n').Append(AsciiTable.Border(lineLength, AsciiTable.BottomLeft, AsciiTable.BottomRight));
            readout.text = text.ToString();
        }

        static string Km(double meters) => $"{meters / 1000.0:N0} km";
    }
}
