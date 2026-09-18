using TMPro;
using UnityEngine;
using Utilities;

namespace Interior
{
    /// <summary>
    /// Малый экран панели: такой же прибор, как навигационный, только показывает строки,
    /// а не обстановку. Своя камера, своя текстура, свой холст — картинка снимается камерой
    /// и кладётся на стекло. Текст поэтому и светится сам, и режется краями стекла, и
    /// задаётся в пикселях текстуры, а не в метрах кабины.
    ///
    /// Что нужно из сцены: меш стекла и образец строки. Как экран выглядит — шрифт, кегль,
    /// цвет, заливка — задаётся образцом и полями, потому что это внешний вид, а внешний вид
    /// правят глазами. Где стоит камера, решает код: её положение ничего не значит.
    ///
    /// Что писать на экран, этот компонент не знает: строки ему кладут соседи по объекту —
    /// <see cref="OrbitReadout"/>, <see cref="TargetListPanel"/>.
    /// </summary>
    public class ReadoutPanel : MonoBehaviour
    {
        const string ScreenLayer = "Panels";

        [Tooltip("Поверхность, на которой видна картинка экрана: любой меш с UV.")]
        public Renderer surface;
        [Tooltip("Разрешение экрана по высоте. Ширина считается из пропорций стекла.")]
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
        [Tooltip("Образец строк: шрифт, кегль, цвет. Пусто — экран останется пустым.")]
        public TextMeshProUGUI textPrefab;
        [Tooltip("Отступ от левого верхнего угла стекла, в пикселях текстуры.")]
        public Vector2 margin = new(12f, 12f);

        /// <summary>
        /// Раз во сколько кадров пересчитывать строки. Показания меняются медленнее картинки,
        /// и собирать их каждый кадр незачем.
        /// </summary>
        public int everyFrames = 10;

        GameObject rig;
        TextMeshProUGUI line;
        RenderTexture texture;

        public string Text
        {
            get => line == null ? "" : line.text;
            set { if (line != null) line.text = value; }
        }

        public bool DueThisFrame => line != null && Time.frameCount % Mathf.Max(everyFrames, 1) == 0;

        void Awake()
        {
            if (surface == null)
            {
                Debug.LogError($"ReadoutPanel на «{name}»: не указано стекло экрана — показывать не на чем.", this);
                enabled = false;
                return;
            }
            if (textPrefab == null)
            {
                Debug.LogError($"ReadoutPanel на «{name}»: не задан образец строк — экран останется пустым.", this);
                enabled = false;
                return;
            }

            if (normalizeScreenUV) ScreenGlass.NormalizeUV(surface, screenRotation, flipScreenU, flipScreenV);
            textureWidth = ScreenGlass.TextureWidth(surface, textureHeight, textureWidth);
            texture = new RenderTexture(textureWidth, textureHeight, 24) { name = $"Readout {name}" };
            Build();
            ScreenGlass.Show(surface, texture);
        }

        void OnDestroy()
        {
            if (rig != null) Destroy(rig);
            if (texture == null) return;
            // Текстура — не объект сцены, её никто не соберёт: RenderTexture держит память
            // на видеокарте, пока её явно не отпустят.
            texture.Release();
            Destroy(texture);
        }

        /// <summary>
        /// Камера и холст создаются кодом, и это не отступление от правила «сцену собирают
        /// руками», а его следствие: место холста не выбирают глазами. Оно выводится из
        /// камеры — ровно перед ней, с масштабом, при котором высота холста равна высоте
        /// текстуры. Только тогда «кегль 18» означает 18 пикселей из 256, а не случайную
        /// долю кадра.
        ///
        /// Объект стоит в корне сцены, а не в детях панели: он не должен ездить вместе с
        /// кораблём и не должен зависеть от масштаба, накопленного родителями.
        /// </summary>
        void Build()
        {
            int layer = LayerMask.NameToLayer(ScreenLayer);
            if (layer < 0)
            {
                Debug.LogError($"ReadoutPanel на «{name}»: в проекте нет слоя «{ScreenLayer}» — " +
                    "камере экрана нечего показывать, кроме чужой обстановки.", this);
                enabled = false;
                return;
            }

            rig = new GameObject($"Readout {name}") { layer = layer };
            rig.transform.position = ScreenGlass.NextRigPosition();

            Camera cam = rig.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = screenBackground;
            cam.cullingMask = 1 << layer;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 10f;
            cam.targetTexture = texture;

            GameObject canvasObject = new("Canvas") { layer = layer };
            canvasObject.transform.SetParent(rig.transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;

            RectTransform rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(textureWidth, textureHeight);
            rect.localPosition = new Vector3(0f, 0f, 1f);
            float scale = 2f * cam.orthographicSize / textureHeight;
            rect.localScale = new Vector3(scale, scale, scale);

            line = Instantiate(textPrefab, canvasObject.transform);
            line.gameObject.name = "Text";
            line.gameObject.layer = layer;
            line.raycastTarget = false;
            // Якоря образца переписываются намеренно: образец не знает и не должен знать
            // размер холста экрана, а позиция, осмысленная на одном, на другом уезжает за
            // край. Размер блока остаётся вашим: его задаёт Size Delta образца.
            RectTransform text = line.rectTransform;
            text.anchorMin = new Vector2(0f, 1f);
            text.anchorMax = new Vector2(0f, 1f);
            text.pivot = new Vector2(0f, 1f);
            text.anchoredPosition = new Vector2(margin.x, -margin.y);
        }
    }
}
