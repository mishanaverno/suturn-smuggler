using UnityEngine;
using Utilities;

namespace Interior
{
    /// <summary>Физическое стекло: его развёртка, пропорции и картинка по умолчанию.</summary>
    public sealed class CockpitScreen : MonoBehaviour
    {
        [Tooltip("Меш стекла, на котором показывать приборы.")]
        public Renderer surface;
        [Tooltip("Картинка на этом стекле при запуске.")]
        public ScreenContent defaultContent;
        public bool normalizeScreenUV = true;
        public ScreenTurn screenRotation = ScreenTurn.Deg0;
        public bool flipScreenU;
        public bool flipScreenV;

        void Awake()
        {
            if (surface == null || defaultContent == ScreenContent.None)
            {
                Debug.LogError($"CockpitScreen на «{name}»: нужны стекло и прибор по умолчанию.", this);
                enabled = false;
                return;
            }

            if (normalizeScreenUV) ScreenGlass.NormalizeUV(surface, screenRotation, flipScreenU, flipScreenV);
            // Меряем именно стекло; разрешение и пропорции источника здесь не участвуют.
            float aspect = ScreenGlass.TextureWidth(surface, 1024, 1024) / 1024f;
            ScreenRouter.RegisterDisplay(surface, defaultContent, aspect);
        }

        void OnDestroy() => ScreenRouter.UnregisterDisplay(surface);
    }
}
