using System.Collections.Generic;
using UnityEngine;

namespace Interior
{
    /// <summary>Виды картинки, которые приборы кабины могут отправить на любое стекло.</summary>
    public enum ScreenContent { None, Docking, Attitude, Maneuver }

    /// <summary>
    /// Соединяет готовые текстуры приборов с физическими экранами. Приборы продолжают
    /// вычислять и рисовать свои данные; переключается только стекло, на которое они выведены.
    ///
    /// Ширина текстуры прибора подгоняется под стекло, на которое её вывели: высота остаётся,
    /// чтобы кегль и линии в пикселях не менялись. Прибор перекладывает раскладку сам, заметив
    /// новую ширину. Если одну картинку показывают два стекла разных пропорций, текстура идёт
    /// по последнему, а на другом вписывается с полями.
    /// </summary>
    public static class ScreenRouter
    {
        sealed class Display
        {
            public ScreenContent defaultContent;
            public ScreenContent selectedContent;
            public float aspect;
        }

        static readonly Dictionary<ScreenContent, RenderTexture> feeds = new();
        static readonly Dictionary<Renderer, Display> displays = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            feeds.Clear();
            displays.Clear();
        }

        public static bool Has(ScreenContent content) =>
            feeds.TryGetValue(content, out RenderTexture texture) && texture != null;

        public static bool CanShow(Renderer glass, ScreenContent content) =>
            glass != null && displays.ContainsKey(glass) && Has(content);

        public static void RegisterFeed(ScreenContent content, RenderTexture texture)
        {
            if (content == ScreenContent.None || texture == null) return;
            feeds[content] = texture;
            foreach (KeyValuePair<Renderer, Display> pair in displays)
                if (pair.Key != null && pair.Value.selectedContent == content)
                    ShowTexture(pair.Key, pair.Value, texture);
        }

        public static void RegisterDisplay(Renderer glass, ScreenContent defaultContent, float aspect)
        {
            if (glass == null || defaultContent == ScreenContent.None) return;
            Shader shader = Resources.Load<Shader>("Shaders/ScreenRoutedSurface");
            if (shader == null)
            {
                Debug.LogError("ScreenRouter: отсутствует шейдер ScreenRoutedSurface.", glass);
                return;
            }
            // Новый материал не наследует цвет и UV-смещение старого материала стекла.
            glass.material = new Material(shader) { name = "Routed Screen" };
            displays[glass] = new Display
            {
                defaultContent = defaultContent,
                selectedContent = defaultContent,
                aspect = Mathf.Max(aspect, 0.001f)
            };
            if (Has(defaultContent)) Show(glass, defaultContent);
            else ShowTexture(glass, displays[glass], Texture2D.blackTexture);
        }

        public static void UnregisterDisplay(Renderer glass)
        {
            if (glass != null) displays.Remove(glass);
        }

        public static void UnregisterFeed(ScreenContent content, RenderTexture texture)
        {
            if (!feeds.TryGetValue(content, out RenderTexture registered) || registered != texture) return;
            feeds.Remove(content);
            foreach (KeyValuePair<Renderer, Display> pair in displays)
            {
                if (pair.Value.selectedContent != content) continue;
                if (pair.Key == null) continue;
                if (Has(pair.Value.defaultContent)) Show(pair.Key, pair.Value.defaultContent);
                else
                {
                    pair.Value.selectedContent = ScreenContent.None;
                    ShowTexture(pair.Key, pair.Value, Texture2D.blackTexture);
                }
            }
        }

        public static bool Show(Renderer glass, ScreenContent content)
        {
            if (!CanShow(glass, content)) return false;
            Display display = displays[glass];
            display.selectedContent = content;
            ShowTexture(glass, display, feeds[content]);
            return true;
        }

        static void ShowTexture(Renderer glass, Display display, Texture texture)
        {
            if (texture is RenderTexture feed && Fit(feed, display.aspect))
            {
                foreach (KeyValuePair<Renderer, Display> pair in displays)
                    if (pair.Key != null && pair.Key != glass && feeds.TryGetValue(pair.Value.selectedContent, out RenderTexture shown) && shown == feed)
                        Frame(pair.Key, pair.Value, feed);
            }
            Frame(glass, display, texture);
        }

        static bool Fit(RenderTexture feed, float aspect)
        {
            int width = Mathf.Max(1, Mathf.RoundToInt(feed.height * aspect));
            if (feed.width == width) return false;
            feed.Release();
            feed.width = width;
            feed.Create();
            // Камера сама не пересчитывает пропорции, когда её текстура меняет размер, и
            // рисовала бы в прежних — края раскладки уходили бы за кадр.
            foreach (Camera cam in Camera.allCameras)
                if (cam.targetTexture == feed) cam.aspect = width / (float)feed.height;
            return true;
        }

        static void Frame(Renderer glass, Display display, Texture texture)
        {
            Material material = glass.material;
            material.mainTexture = texture;
            float sourceAspect = texture.width / (float)texture.height;
            float scaleX = Mathf.Max(1f, display.aspect / sourceAspect);
            float scaleY = Mathf.Max(1f, sourceAspect / display.aspect);
            material.mainTextureScale = new Vector2(scaleX, scaleY);
            material.mainTextureOffset = new Vector2((1f - scaleX) * .5f, (1f - scaleY) * .5f);
        }
    }
}
