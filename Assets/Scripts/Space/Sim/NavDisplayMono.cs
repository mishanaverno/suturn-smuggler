using UnityEngine;
using UnityEngine.UI;

namespace OuterSpace.Sim
{
    /// <summary>
    /// Экран навигационного компьютера корабля: прибор внутри игрового мира, а не свободный
    /// вид космоса. Своя камера, своя текстура фиксированного разрешения, свой ввод.
    /// Пока рубки нет, текстура выводится отладочным полноэкранным видом; когда появится
    /// панель, та же текстура вешается на неё и здесь ничего не меняется.
    ///
    /// Камера ортографическая: в перспективе две одинаковые метки на разной глубине имели бы
    /// разный экранный размер, и промежуточный размер снова начал бы что-то означать.
    /// </summary>
    public class NavDisplayMono : MonoBehaviour
    {
        // Дальность - половина высоты видимой области в метрах. Фиксированный набор удобнее
        // плавного зума: игрок запоминает диапазоны, а подписи под ними честные.
        public static readonly double[] Ranges = {
            1.3e10, // вся система, орбита Фебы
            3.7e9,  // до Япета
            1.3e9,  // до Титана
            5.0e7,  // сфера влияния Титана
            5.0e6,  // окрестности Титана
        };
        public const double DefaultRange = 1.3e9;

        public static NavDisplayMono instance;

        public int textureWidth = 1024;
        public int textureHeight = 768;
        // 12 пикселей из 768 по высоте экрана прибора.
        public double markerFraction = 12.0 / 768.0;
        public double labelFraction = 18.0 / 768.0;
        public double lineFraction = 2.0 / 768.0;
        public int rangeIndex = 2;

        public RenderTexture texture { get; private set; }
        public Camera cam { get; private set; }
        /// <summary>Объект, на котором стоит начало сцены: вокруг него вращается вид.</summary>
        public SpaceObject focus;

        float yaw = 0f;
        float pitch = 60f;
        int focusIndex = -1;
        int targetIndex = -1;

        public double Range => Ranges[rangeIndex];
        public double MetersPerPixel => NavScale.MetersPerPixel(NavScale.OrthographicSize, SimView.metersPerSceneUnit, textureHeight);
        public double MarkerSceneDiameter => NavScale.MarkerSceneDiameter(markerFraction, NavScale.OrthographicSize);
        public double LabelSceneHeight => NavScale.MarkerSceneDiameter(labelFraction, NavScale.OrthographicSize);
        public float LineSceneWidth => (float)NavScale.MarkerSceneDiameter(lineFraction, NavScale.OrthographicSize);

        void Awake()
        {
            instance = this;
            SimView.metersPerSceneUnit = NavScale.MetersPerSceneUnit(Range);

            texture = new RenderTexture(textureWidth, textureHeight, 24) { name = "NavDisplay" };

            GameObject camObject = new("NavCamera");
            camObject.transform.parent = transform;
            cam = camObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = (float)NavScale.OrthographicSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            // Прибор видит только слой симуляции, и ничего кроме него нарисовать на нём
            // технически невозможно.
            cam.cullingMask = 1 << LayerMask.NameToLayer("Simulation");
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 200f;
            cam.targetTexture = texture;

            CreateDebugView();
        }

        void CreateDebugView()
        {
            GameObject canvasObject = new("NavDisplayDebugView");
            canvasObject.transform.parent = transform;
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100;

            GameObject imageObject = new("Screen");
            imageObject.transform.SetParent(canvasObject.transform, false);
            RawImage image = imageObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // Update, а не LateUpdate: SimMono двигает тела в FixedUpdate, то есть до Update
        // текущего кадра, а орбиты и сферы влияния строятся в LateUpdate. Перепроецировать
        // сцену надо между тем и другим, иначе линии окажутся построены в старом масштабе.
        void Update()
        {
            if (SimMono.playerShip == null) return;
            ReadInput();

            focus ??= SimMono.playerShip;
            SimView.origin = focus.simTransform.GLOBAL_R;
            SimView.metersPerSceneUnit = NavScale.MetersPerSceneUnit(Range);
            foreach (SpaceObject obj in SimMono.updateOrder) obj.simTransform.Reproject();

            cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            cam.transform.position = cam.transform.rotation * Vector3.back * 100f;
        }

        void ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.LeftBracket)) rangeIndex = Mathf.Max(rangeIndex - 1, 0);
            if (Input.GetKeyDown(KeyCode.RightBracket)) rangeIndex = Mathf.Min(rangeIndex + 1, Ranges.Length - 1);

            if (Input.GetKey(KeyCode.LeftArrow)) yaw -= 60f * Time.deltaTime;
            if (Input.GetKey(KeyCode.RightArrow)) yaw += 60f * Time.deltaTime;
            if (Input.GetKey(KeyCode.UpArrow)) pitch = Mathf.Min(pitch + 60f * Time.deltaTime, 89f);
            if (Input.GetKey(KeyCode.DownArrow)) pitch = Mathf.Max(pitch - 60f * Time.deltaTime, -89f);

            if (Input.GetKeyDown(KeyCode.Tab)) CycleFocus();
            if (Input.GetKeyDown(KeyCode.T)) CycleTarget();
        }

        void CycleTarget()
        {
            targetIndex++;
            if (targetIndex >= SimMono.bodies.Count) targetIndex = -1;
            SimMono.target = targetIndex < 0 ? null : SimMono.bodies[targetIndex];
        }

        void CycleFocus()
        {
            focusIndex++;
            if (focusIndex >= SimMono.updateOrder.Count) focusIndex = -1;
            focus = focusIndex < 0 ? SimMono.playerShip : SimMono.updateOrder[focusIndex];
        }
    }
}
