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
    /// Текстовый экран бортовых часов: сатурнианские сутки, время суток, секундомер и список
    /// таймеров вместе с метками времени навигационного экрана.
    /// Сам создаёт камеру, холст и текстуру и отдаёт её в ScreenRouter.
    ///
    /// Время суток — обычные ч:мм:сс, которые обнуляются в 10:33:38: все остальные приборы
    /// считают в секундах, и пересчитывать в голове «сатурнианские часы» незачем.
    /// </summary>
    public class ClockPanel : MonoBehaviour
    {
        const string ScreenLayer = "Panels";
        const string Title = "CLOCK";

        [Tooltip("Разрешение изображения прибора по высоте. Ширина подгоняется под стекло.")]
        public int textureHeight = 360;
        public Color screenBackground = Color.black;

        [Tooltip("Размер шрифта в пикселях текстуры. Кегль образца заменяется этим значением.")]
        public float labelPixels = 18f;
        [Tooltip("Отступ текста от верхнего левого угла, в пикселях текстуры.")]
        public Vector2 margin = new(12f, 12f);
        [Tooltip("Интервал обновления показаний в кадрах.")]
        public int everyFrames = 10;
        [Tooltip("Ширина рамки в знаках. Ноль меряет шаг знака у шрифта и растягивает рамку по стеклу.")]
        public int lineColumns;

        readonly struct Row
        {
            public readonly double Epoch;
            public readonly string Label;
            public readonly Color Color;
            /// <summary>null — метка навигационного экрана, а не таймер.</summary>
            public readonly Timers.Timer Timer;

            public Row(double epoch, string label, Color color, Timers.Timer timer = null)
            {
                Epoch = epoch;
                Label = label;
                Color = color;
                Timer = timer;
            }
        }

        readonly StringBuilder builder = new();
        readonly List<Row> rows = new();
        GameObject rig;
        RenderTexture texture;
        RectTransform canvasRect;
        TextMeshProUGUI readout;
        int laidOutWidth;
        int columns;
        int scrollOffset;

        void Awake()
        {
            if (NavPalette.ReadoutPrefab == null)
            {
                Debug.LogError($"ClockPanel на «{name}»: в NavPalette не задан образец строки показаний.", this);
                enabled = false;
                return;
            }

            int layer = LayerMask.NameToLayer(ScreenLayer);
            if (layer < 0)
            {
                Debug.LogError($"ClockPanel на «{name}»: отсутствует слой «{ScreenLayer}».", this);
                enabled = false;
                return;
            }

            texture = new RenderTexture(textureHeight, textureHeight, 24) { name = $"Clock {name}" };
            Build(layer);
            ScreenRouter.RegisterFeed(ScreenContent.Clock, texture);
            Layout();
            Refresh();
        }

        void OnDestroy()
        {
            ScreenRouter.UnregisterFeed(ScreenContent.Clock, texture);
            if (rig != null) Destroy(rig);
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
        }

        void Build(int layer)
        {
            rig = new GameObject($"Clock {name}") { layer = layer };
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

            canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.localPosition = new Vector3(0f, 0f, 1f);

            readout = Instantiate(NavPalette.ReadoutPrefab, canvasObject.transform);
            readout.gameObject.name = "Readout";
            readout.gameObject.layer = layer;
            readout.raycastTarget = false;
            readout.fontSize = Mathf.Max(labelPixels, 1f);
            readout.textWrappingMode = TextWrappingModes.NoWrap;

            RectTransform textRect = readout.rectTransform;
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(0f, 1f);
            textRect.pivot = new Vector2(0f, 1f);
            textRect.anchoredPosition = new Vector2(margin.x, -margin.y);
        }

        void Layout()
        {
            laidOutWidth = texture.width;
            canvasRect.sizeDelta = new Vector2(texture.width, textureHeight);
            RectTransform textRect = readout.rectTransform;
            textRect.sizeDelta = new Vector2(
                Mathf.Max(1f, texture.width - 2f * margin.x),
                Mathf.Max(1f, textureHeight - 2f * margin.y));
            columns = lineColumns > 0 ? lineColumns : NavText.Columns(readout, textRect.sizeDelta.x);
        }

        void Update()
        {
            if (texture.width != laidOutWidth)
            {
                Layout();
                Refresh();
                return;
            }
            if (Time.frameCount % Mathf.Max(everyFrames, 1) == 0) Refresh();
        }

        void Refresh()
        {
            if (GameMono.instance == null) return;
            double now = GameMono.instance.Epoch;
            Timers timers = GameMono.instance.timers;

            long day = (long)(now / Timers.DayLength);
            string dayLine = $"SD {day + 1:0000}";
            string timeLine = TrajectoryRenderer.Clock(now - day * Timers.DayLength);
            string watchLine = timers.StopwatchRunning
                ? TrajectoryRenderer.Clock(now - timers.StopwatchStart)
                : "--:--:--";

            int lineLength = Mathf.Max(columns, dayLine.Length + timeLine.Length + 5);
            lineLength = Mathf.Max(lineLength, Title.Length + 2);
            string dateRow = dayLine + timeLine.PadLeft(lineLength - 4 - dayLine.Length);
            string watchRow = "SW" + watchLine.PadLeft(lineLength - 6);

            Color label = NavText.Label;
            builder.Clear();
            builder.Append(NavText.Paint(AsciiTable.TitledBorder(Title, lineLength), label));
            builder.Append('\n').Append(NavText.Paint(AsciiTable.Row(dateRow, lineLength), NavPalette.Own));
            builder.Append('\n').Append(NavText.Paint(AsciiTable.Row(watchRow, lineLength),
                timers.StopwatchRunning ? NavPalette.Own : label));
            builder.Append('\n').Append(NavText.Paint(AsciiTable.ColumnsBorder(new[] { lineLength - 4 }, AsciiTable.Cross), label));

            CollectRows(timers, now);
            int count = rows.Count;
            if (count == 0)
            {
                builder.Append('\n').Append(NavText.Paint(AsciiTable.Row("NO TIMERS", lineLength), label));
            }

            int visible = VisibleRows();
            int cursor = rows.FindIndex(r => r.Timer != null && r.Timer == timers.Selected);
            if (cursor >= 0 && cursor < scrollOffset) scrollOffset = cursor;
            else if (cursor >= scrollOffset + visible) scrollOffset = cursor - visible + 1;
            scrollOffset = Mathf.Clamp(scrollOffset, 0, Mathf.Max(0, count - visible));

            // Сработавший мигает: он ждёт, пока его снимут, и должен быть виден краем глаза.
            bool blink = Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f;
            int end = Mathf.Min(scrollOffset + visible, count);
            // Отсчёты — одним столбцом, иначе разной длины подписи разносят их по строке.
            int labelWidth = 0;
            for (int i = scrollOffset; i < end; i++) labelWidth = Mathf.Max(labelWidth, rows[i].Label.Length);
            for (int i = scrollOffset; i < end; i++)
            {
                Row row = rows[i];
                string line = $"{(i == cursor ? '>' : ' ')} {row.Label.PadRight(labelWidth)} {TrajectoryRenderer.Countdown(row.Epoch - now)}";
                Color color = row.Timer == null ? row.Color
                    : row.Timer.Fired ? (blink ? NavPalette.Alarm : NavPalette.Dim(NavPalette.Alarm, NavPalette.NameLevel))
                    : i == cursor ? NavPalette.Own : label;
                builder.Append('\n').Append(NavText.Paint(AsciiTable.Row(line, lineLength), color));
            }
            builder.Append('\n').Append(NavText.Paint(
                AsciiTable.Border(lineLength, AsciiTable.BottomLeft, AsciiTable.BottomRight), label));
            readout.text = NavText.Frame(builder.ToString());
        }

        /// <summary>
        /// Таймеры и метки времени навигационного экрана одним списком по времени: узлы плана,
        /// смены сферы влияния и сближения с целью — с теми же цветами, что на экране. Меток
        /// курсор не касается: снять или сдвинуть их нельзя, они следуют из плана.
        /// </summary>
        void CollectRows(Timers timers, double now)
        {
            rows.Clear();
            foreach (Timers.Timer timer in timers.List)
                rows.Add(new Row(timer.Epoch, $"T{timer.Number}", default, timer));

            if (SimMono.playerShip is Ship ship)
            {
                AddTrajectory(ship.trajectory, "", NavPalette.Own, now);
                List<Maneuver> plan = ship.Maneuvers();
                for (int i = 0; i < plan.Count; i++)
                {
                    Color color = NavPalette.Maneuver(i);
                    rows.Add(new Row(plan[i].startEpoch, $"M{i + 1} NODE", color));
                    AddTrajectory(plan[i].trajectory, $"M{i + 1} ", color, now);
                }
            }
            rows.Sort((a, b) => a.Epoch.CompareTo(b.Epoch));
        }

        void AddTrajectory(TrajectoryCache trajectory, string prefix, Color color, double now)
        {
            if (trajectory.patches != null)
            {
                foreach (TrajectoryPatch patch in trajectory.patches)
                {
                    if (patch.EndReason == PatchEndReason.Horizon || patch.EndEpoch <= now) continue;
                    string text = patch.EndReason == PatchEndReason.Impact
                        ? "IMPACT"
                        : $"SOI {patch.NextCentral.GameObject.name.ToUpperInvariant()}";
                    rows.Add(new Row(patch.EndEpoch, prefix + text, NavPalette.Alarm));
                }
            }
            if (trajectory.approaches == null) return;
            int shown = 0;
            foreach (CloseApproach approach in trajectory.approaches)
            {
                if (approach.Epoch <= now) continue;
                if (shown++ >= TrajectoryRenderer.ShownApproaches) break;
                rows.Add(new Row(approach.Epoch, $"{prefix}APPR {approach.Distance / 1000.0:F1} km", color));
            }
        }

        /// <summary>Строк под список: всё, кроме рамки, строк даты и секундомера и разделителя.</summary>
        int VisibleRows()
        {
            float height = Mathf.Max(1f, textureHeight - 2f * margin.y);
            int lines = Mathf.FloorToInt(height / (Mathf.Max(labelPixels, 1f) * 1.2f));
            return Mathf.Max(1, lines - 5);
        }
    }
}
