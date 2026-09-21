using System.Collections.Generic;
using System.Text;
using OuterSpace;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using TMPro;
using UnityEngine;
using Utilities;

namespace Interior
{
    /// <summary>
    /// Самостоятельный текстовый экран со списком известных целей. Сам создаёт камеру,
    /// холст и текстуру для стекла. Курсор списка и выбранная цель независимы: курсор
    /// только указывает, над каким телом сработают кнопки выбора и слежения.
    /// </summary>
    public class TargetListPanel : MonoBehaviour
    {
        const string ScreenLayer = "Panels";

        enum ListMode { Bodies, Maneuvers }

        readonly struct ListEntry
        {
            public readonly string Label;
            public readonly SimTransform Focus;
            public readonly SpaceObject Target;
            /// <summary>Роль строки: тем же цветом эта же вещь нарисована на навигационном экране.</summary>
            public readonly Color Role;

            public ListEntry(string label, SimTransform focus, Color role, SpaceObject target = null)
            {
                Label = label;
                Focus = focus;
                Role = role;
                Target = target;
            }
        }

        public static TargetListPanel instance { get; private set; }

        [Tooltip("Поверхность экрана: меш с UV-развёрткой.")]
        public Renderer surface;
        [Tooltip("Разрешение экрана по высоте. Ширина берётся из пропорций стекла.")]
        public int textureHeight = 256;
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
        public float labelPixels = 24f;
        [Tooltip("Сколько целей помещать на экран. Ноль вычисляет число строк из высоты экрана и размера шрифта.")]
        public int visibleTargets;
        [Tooltip("Отступ текста от верхнего левого угла, в пикселях текстуры.")]
        public Vector2 margin = new(12f, 12f);
        [Tooltip("Интервал обновления списка в кадрах.")]
        public int everyFrames = 10;
        [Tooltip("Ширина рамки в знаках. Ноль меряет шаг знака у шрифта и растягивает рамку по стеклу.")]
        public int lineColumns;

        readonly StringBuilder builder = new();
        readonly List<ListEntry> entries = new();
        readonly List<Maneuver> maneuvers = new();
        GameObject rig;
        RenderTexture texture;
        TextMeshProUGUI readout;
        ListMode mode;
        /// <summary>Ширина стекла в знаках: рамка растягивается на неё, а не на длину имени.</summary>
        int columns;
        int cursorIndex = -1;
        int scrollOffset;

        public bool HasCursor => CursorEntry().Focus != null;
        public bool CanSelectCursor => CursorEntry().Target != null;

        void Awake()
        {
            if (surface == null || textPrefab == null)
            {
                Debug.LogError($"TargetListPanel на «{name}»: укажите стекло и образец текста.", this);
                enabled = false;
                return;
            }

            int layer = LayerMask.NameToLayer(ScreenLayer);
            if (layer < 0)
            {
                Debug.LogError($"TargetListPanel на «{name}»: отсутствует слой «{ScreenLayer}».", this);
                enabled = false;
                return;
            }

            instance = this;
            if (normalizeScreenUV) ScreenGlass.NormalizeUV(surface, screenRotation, flipScreenU, flipScreenV);
            textureWidth = ScreenGlass.TextureWidth(surface, textureHeight, textureWidth);
            texture = new RenderTexture(textureWidth, textureHeight, 24) { name = $"Targets {name}" };
            Build(layer);
            ScreenGlass.Show(surface, texture);
            Refresh();
        }

        void OnDestroy()
        {
            if (ReferenceEquals(instance, this)) instance = null;
            if (rig != null) Destroy(rig);
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
        }

        void Build(int layer)
        {
            rig = new GameObject($"Targets {name}") { layer = layer };
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
            readout.textWrappingMode = TextWrappingModes.NoWrap;

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

        public void MoveCursor(int direction)
        {
            BuildEntries();
            int count = entries.Count;
            if (count == 0)
            {
                cursorIndex = -1;
                return;
            }

            if (cursorIndex < 0 || cursorIndex >= count)
            {
                cursorIndex = direction < 0 ? count - 1 : 0;
            }
            else
            {
                cursorIndex = (cursorIndex + (direction < 0 ? -1 : 1) + count) % count;
            }
            Refresh();
        }

        public void SelectCursor()
        {
            SpaceObject target = CursorEntry().Target;
            if (target == null) return;
            SimMono.target = target;
            Refresh();
        }

        public void ClearTarget()
        {
            SimMono.target = null;
            Refresh();
        }

        public void TrackCursor()
        {
            SimTransform focus = CursorEntry().Focus;
            if (focus != null) NavDisplayPanel.instance?.FocusOn(focus);
        }

        public void ShowBodies()
        {
            SetMode(ListMode.Bodies);
        }

        public void ShowManeuvers()
        {
            SetMode(ListMode.Maneuvers);
        }

        void SetMode(ListMode value)
        {
            if (mode == value) return;
            mode = value;
            cursorIndex = 0;
            scrollOffset = 0;
            Refresh();
        }

        void Refresh()
        {
            BuildEntries();
            int count = entries.Count;
            if (count == 0) cursorIndex = -1;
            else if (cursorIndex < 0 || cursorIndex >= count) cursorIndex = 0;

            int rows = VisibleTargetCount();
            int maxOffset = Mathf.Max(0, count - rows);
            if (cursorIndex < scrollOffset)
            {
                scrollOffset = cursorIndex;
            }
            else if (cursorIndex >= scrollOffset + rows)
            {
                scrollOffset = cursorIndex - rows + 1;
            }
            scrollOffset = Mathf.Clamp(scrollOffset, 0, maxOffset);

            int end = Mathf.Min(scrollOffset + rows, count);
            string title = mode == ListMode.Bodies ? "BODIES" : "MANEUVERS";
            if (count > rows) title += $" {scrollOffset + 1}-{end}/{count}";

            // Ширина рамки — по стеклу, но не уже самой длинной строки внутри: список
            // меняется в полёте, и имя длиннее экрана резать нечем.
            int lineLength = Mathf.Max(columns, title.Length + 2);
            for (int i = scrollOffset; i < end; i++)
            {
                lineLength = Mathf.Max(lineLength, entries[i].Label.Length + 6);
            }
            if (count == 0) lineLength = Mathf.Max(lineLength, "NO DATA".Length + 4);

            Color label = NavText.Label;
            builder.Clear();
            builder.Append(NavText.Paint(AsciiTable.TitledBorder(title, lineLength), label));
            if (count == 0)
            {
                builder.Append('\n').Append(NavText.Paint(AsciiTable.Row("NO DATA", lineLength), label));
            }
            for (int i = scrollOffset; i < end; i++)
            {
                ListEntry entry = entries[i];
                // Выбранная цель отмечена цветом роли — тем же, каким она нарисована на
                // навигационном экране. Звёздочка рядом говорила бы то же самое второй раз.
                string line = $"{(i == cursorIndex ? '>' : ' ')} {entry.Label}";
                // Строка под курсором горит в полную силу своей роли, остальные — как имена:
                // курсор это «куда я сейчас смотрю», и он должен читаться, не меняя цвета,
                // которым эта же вещь нарисована на навигационном экране.
                Color row = i == cursorIndex ? entry.Role : NavPalette.Dim(entry.Role, NavPalette.NameLevel);
                builder.Append('\n').Append(NavText.Paint(AsciiTable.Row(line, lineLength), row));
            }
            builder.Append('\n').Append(NavText.Paint(
                AsciiTable.Border(lineLength, AsciiTable.BottomLeft, AsciiTable.BottomRight), label));
            readout.text = NavText.Frame(builder.ToString());
        }

        ListEntry CursorEntry()
        {
            BuildEntries();
            return cursorIndex >= 0 && cursorIndex < entries.Count ? entries[cursorIndex] : default;
        }

        void BuildEntries()
        {
            entries.Clear();
            if (mode == ListMode.Bodies)
            {
                // Корневое тело системы (Сатурн) хранится отдельно в SimMono.root, поэтому
                // SimMono.bodies уже содержит ровно те тела, которые разрешено выбрать целью.
                foreach (SpaceObject body in SimMono.bodies)
                {
                    entries.Add(new ListEntry(body.GameObject.name.ToUpperInvariant(), body.simTransform,
                        NavPalette.For(body), body));
                }
                return;
            }

            if (SimMono.playerShip is not Ship ship) return;
            entries.Add(new ListEntry("SHIP", ship.simTransform, NavPalette.Own));

            maneuvers.Clear();
            for (Maneuver maneuver = ship.GetManeuver(); maneuver != null; maneuver = maneuver.Previous)
            {
                maneuvers.Add(maneuver);
            }
            maneuvers.Reverse();
            for (int i = 0; i < maneuvers.Count; i++)
            {
                entries.Add(new ListEntry($"MANEUVER {i + 1}", maneuvers[i].simTransform,
                    NavPalette.Maneuver(i)));
            }
        }

        int VisibleTargetCount()
        {
            if (visibleTargets > 0) return visibleTargets;
            // Две строки заняты рамкой: заголовок врезан в верхнюю, нижняя закрывает список.
            // Коэффициент 1.2 оставляет место под обычный межстрочный интервал TMP, чтобы
            // последняя строка не обрезалась краем экрана.
            float height = Mathf.Max(1f, textureHeight - 2f * margin.y);
            int lines = Mathf.FloorToInt(height / (Mathf.Max(labelPixels, 1f) * 1.2f));
            return Mathf.Max(1, lines - 2);
        }
    }
}
