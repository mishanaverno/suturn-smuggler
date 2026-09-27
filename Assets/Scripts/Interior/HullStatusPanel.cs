using System.Text;
using DoublePrecision;
using OuterSpace.Sim;
using TMPro;
using UnityEngine;
using Utilities;

namespace Interior
{
    /// <summary>
    /// Самостоятельный текстовый экран корпуса на своём постоянном стекле: температура и износ
    /// шести панелей строкой-шкалой и процентом.
    ///
    /// Показания берутся со щитка, а не из корабля: снятое или неисправное устройство корпуса
    /// гасит экран, как гасит любое табло.
    /// </summary>
    public class HullStatusPanel : MonoBehaviour
    {
        const string ScreenLayer = "Panels";
        const string Title = "HULL";
        const int NameWidth = 6;
        // Рамка, пробелы вокруг имени и шкалы, скобки шкалы и «100%».
        const int FixedWidth = 4 + NameWidth + 1 + 2 + 1 + 4;
        const int MinBar = 10;

        static readonly (string name, ReadingId reading)[] Sections =
        {
            ("NOSE", ReadingId.HullNose),
            ("TAIL", ReadingId.HullTail),
            ("LEFT", ReadingId.HullLeft),
            ("RIGHT", ReadingId.HullRight),
            ("TOP", ReadingId.HullTop),
            ("BOTTOM", ReadingId.HullBottom),
        };

        [Tooltip("Постоянное стекло экрана корпуса: меш с UV-развёрткой.")]
        public Renderer surface;
        [Tooltip("Разрешение изображения прибора по высоте.")]
        public int textureHeight = 256;
        [Tooltip("Разрешение изображения прибора по ширине.")]
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

        readonly double[] wear = new double[Sections.Length];
        GameObject rig;
        RenderTexture texture;
        TextMeshProUGUI readout;
        /// <summary>Ширина стекла в знаках: рамка растягивается на неё.</summary>
        int columns;

        void Awake()
        {
            if (surface == null || NavPalette.ReadoutPrefab == null)
            {
                Debug.LogError($"HullStatusPanel на «{name}»: нужны стекло и образец строки показаний в NavPalette.", this);
                enabled = false;
                return;
            }

            int layer = LayerMask.NameToLayer(ScreenLayer);
            if (layer < 0)
            {
                Debug.LogError($"HullStatusPanel на «{name}»: отсутствует слой «{ScreenLayer}».", this);
                enabled = false;
                return;
            }

            if (normalizeScreenUV) ScreenGlass.NormalizeUV(surface, screenRotation, flipScreenU, flipScreenV);
            textureWidth = ScreenGlass.TextureWidth(surface, textureHeight, textureWidth);
            texture = new RenderTexture(textureWidth, textureHeight, 24) { name = $"Hull {name}" };
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
            rig = new GameObject($"Hull {name}") { layer = layer };
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
            textRect.sizeDelta = new Vector2(
                Mathf.Max(1f, textureWidth - 2f * margin.x),
                Mathf.Max(1f, textureHeight - 2f * margin.y));
            columns = lineColumns > 0 ? lineColumns : NavText.Columns(readout, textRect.sizeDelta.x);
        }

        void Update()
        {
            if (Time.frameCount % Mathf.Max(everyFrames, 1) == 0) Refresh();
        }

        void Refresh()
        {
            int lineLength = Mathf.Max(FixedWidth + MinBar, columns);
            Color label = NavText.Label;

            bool hasTemperature = ControlBus.TryRead(ReadingId.HullTemperature, out double temperature) && !double.IsNaN(temperature);
            for (int i = 0; i < Sections.Length; i++)
            {
                if (hasTemperature && ControlBus.TryRead(Sections[i].reading, out wear[i]) && !double.IsNaN(wear[i])) continue;
                readout.text = NavText.Frame(
                    NavText.Paint(AsciiTable.TitledBorder(Title, lineLength), label) + "\n" +
                    NavText.Paint(AsciiTable.Row("NO DATA", lineLength), label) + "\n" +
                    NavText.Paint(AsciiTable.Border(lineLength, AsciiTable.BottomLeft, AsciiTable.BottomRight), label));
                return;
            }

            int bar = lineLength - FixedWidth;
            StringBuilder text = new(NavText.Paint(AsciiTable.TitledBorder(Title, lineLength), label));
            text.Append('\n').Append(AsciiTable.V).Append(' ')
                .Append(NavText.Paint("TEMP".PadRight(NameWidth), label)).Append(' ')
                .Append(NavText.Paint($"{temperature:F0} K".PadRight(lineLength - NameWidth - 5), NavPalette.Own)).Append(' ')
                .Append(AsciiTable.V);
            text.Append('\n').Append(NavText.Paint(AsciiTable.ColumnsBorder(new[] { lineLength - 4 }, AsciiTable.Cross), label));
            for (int i = 0; i < Sections.Length; i++)
            {
                int filled = Mathd.RoundToInt(wear[i] * bar);
                string gauge = $"[{new string('#', filled)}{new string('-', bar - filled)}] {Mathd.RoundToInt(wear[i] * 100.0),3}%";
                text.Append('\n').Append(AsciiTable.V).Append(' ')
                    .Append(NavText.Paint(Sections[i].name.PadRight(NameWidth), label)).Append(' ')
                    .Append(NavText.Paint(gauge, NavPalette.Own)).Append(' ')
                    .Append(AsciiTable.V);
            }
            text.Append('\n').Append(NavText.Paint(
                AsciiTable.Border(lineLength, AsciiTable.BottomLeft, AsciiTable.BottomRight), label));
            readout.text = NavText.Frame(text.ToString());
        }
    }
}
