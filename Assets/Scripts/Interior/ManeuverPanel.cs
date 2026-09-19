using Game;
using OuterSpace;
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

        void Refresh()
        {
            Ship ship = SimMono.playerShip as Ship;
            Maneuver maneuver = ship?.GetNextManeuver();
            if (maneuver == null)
            {
                readout.text = "NO MANEUVER DATA";
                return;
            }

            double epoch = GameMono.instance.Epoch;
            readout.text =
                $"BURN > T- {TrajectoryRenderer.Clock(ship.BurnStartEpoch - epoch)}\n" +
                $"NODE > T- {TrajectoryRenderer.Clock(maneuver.startEpoch - epoch)}\n" +
                $"DV {maneuver.PlannedMagnitude-ship.BurnedDeltaV:F1}/{maneuver.PlannedMagnitude:F1} m/s\n" +
                "--CURRENT ORBIT----\n" + Orbit(ship.orbitParams) +
                "--TARGET ORBIT-----\n" + Orbit(maneuver.newOrbitParams);
        }

        static string Orbit(OrbitElements orbit)
        {
            (double periapsis, double apoapsis) = AstroDynamic.GetPeriapsisAndApoapsis(orbit);
            return $"PE: {Km(periapsis)} " +
                $"AP: {Km(apoapsis)}\n" +
                $"E: {orbit.eccentricity:F4} " +
                $"I: {orbit.inclination:F2}°\n";
        }

        static string Km(double meters) => $"{meters / 1000.0:N0} km";
    }
}
