using System;
using System.Collections.Generic;
using DoublePrecision;
using Game;
using Interior;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using UnityEngine;

namespace OuterSpace
{
    [Serializable]
    public class MoonStyle
    {
        [Tooltip("Имя тела в системе, как в saturn.json.")]
        public string body;
        public Material material;
    }

    /// <summary>
    /// Космос за иллюминаторами. Отдельная сцена на слое Exterior, которую своя камера рисует
    /// фоном под глазом пилота или тела; интерьер поверх неё только дописывает глубину.
    ///
    /// Floating origin: корабль всегда стоит в нуле этой сцены. Положения тел вычитаются из
    /// положения корабля в double и только разность уходит во float — иначе на расстояниях
    /// в миллиард метров float дрожал бы на сотни метров. Затем всё уменьшается в Scale раз:
    /// расстояние и радиус вместе, поэтому угловой размер тела остаётся истинным, а
    /// дальняя плоскость камеры укладывается в разумное число.
    /// </summary>
    public class ExteriorView : MonoBehaviour
    {
        /// <summary>Метров симуляции в единице внешней сцены: одна единица — километр.</summary>
        const double Scale = 1e3;
        // 100 м: ближе к поверхности корабль не бывает — посадок в игре нет.
        const float Near = 0.1f;
        // 30 млн км: Феба с противоположной стороны системы.
        const float Far = 3e7f;
        const double SunRadius = 6.957e8;
        // Ближний план — станции и всё, к чему подходят, — отдельной сценой в метрах: в
        // километровой станция в 50 м короче ближней плоскости, а опустить её нельзя, не
        // потеряв точность глубины на тридцати миллионах километров.
        const float ProximityNear = 0.1f;
        const float ProximityFar = 5e4f;

        [Tooltip("Оси корабля в интерьере: forward — продольная ось, куда смотрит тяга; up — над головой пилота.")]
        public Transform hull;
        [Tooltip("Образец тела: сфера диаметром 1 с материалом. Масштаб выставляется под размер тела.")]
        public GameObject bodyTemplate;
        [Header("Exterior style")]
        [Tooltip("Материал Сатурна за иллюминатором.")]
        public Material saturnMaterial;
        [Tooltip("Материал ледяных лун, у которых нет своего стиля.")]
        public Material moonMaterial;
        [Tooltip("Свой материал для отдельных лун.")]
        public MoonStyle[] moonStyles = Array.Empty<MoonStyle>();
        [Tooltip("Тёплый материал Титана за иллюминатором.")]
        public Material titanMaterial;
        [Tooltip("Отдельные материалы колец A–G Сатурна.")]
        public RingStyleSet rings = new();
        [Tooltip("Солнце: направленный свет, который видит только слой Exterior. Поворачивается само.")]
        public Light sun;
        [Tooltip("Звёздное небо и солнечный диск — фон внешней камеры.")]
        public Material skyMaterial;

        Transform root;
        Camera cam;
        Camera skyCam;
        Transform proximityRoot;
        Camera proximityCam;
        Camera dockingCam;
        readonly List<(Station station, Transform view)> stationViews = new();
        /// <summary>Модели станций в сцене ближнего плана. Заводятся в первом кадре.</summary>
        public IReadOnlyList<(Station station, Transform view)> StationViews => stationViews;
        readonly List<(SpaceObject body, Transform view, ObjectData data)> views = new();
        static readonly int SaturnDirectionId = Shader.PropertyToID("_SaturnDirection");
        Transform titanView;
        Renderer titanRenderer;
        MaterialPropertyBlock titanProperties;
        readonly TitanFieldBake titanFields = new();
        readonly List<(SpaceObject body, Renderer renderer)> moons = new();
        MaterialPropertyBlock moonProperties;
        static readonly int LeadingId = Shader.PropertyToID("_Leading");
        // Копия skyMaterial: направления меняются каждый кадр и не должны писаться в ассет.
        Material sky;
        static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");
        static readonly int SunAngularRadiusId = Shader.PropertyToID("_SunAngularRadius");
        static readonly int SkyXId = Shader.PropertyToID("_SkyX");
        static readonly int SkyYId = Shader.PropertyToID("_SkyY");
        static readonly int SkyZId = Shader.PropertyToID("_SkyZ");
        // Тела для тени в шейдерах, см. Eclipse.cginc. Размер массива там же: глобальный массив
        // заводится длиной первой записи, поэтому пишется всегда целиком.
        const int EclipseMaxBodies = 16;
        readonly Vector4[] eclipseBodies = new Vector4[EclipseMaxBodies];
        static readonly int EclipseId = Shader.PropertyToID("_Eclipse");
        static readonly int EclipseBodiesId = Shader.PropertyToID("_EclipseBodies");
        static readonly int EclipseBodyCountId = Shader.PropertyToID("_EclipseBodyCount");
        static readonly int EclipseSunDirectionId = Shader.PropertyToID("_EclipseSunDirection");
        static readonly int EclipseSunRadiusId = Shader.PropertyToID("_EclipseSunRadius");
        static readonly int EclipseExteriorScaleId = Shader.PropertyToID("_EclipseExteriorScale");

        void Awake()
        {
            if (hull == null || bodyTemplate == null
                || LayerMask.NameToLayer("Exterior") < 0 || LayerMask.NameToLayer("Proximity") < 0)
            {
                Debug.LogError($"{GetType().Name} на «{name}»: нужны hull, bodyTemplate и слои Exterior, Proximity.", this);
                enabled = false;
                return;
            }
            int layer = LayerMask.NameToLayer("Exterior");
            root = new GameObject("Exterior") { layer = layer }.transform;
            root.SetParent(transform, false);
            CreateCamera(layer);
            CreateProximity(LayerMask.NameToLayer("Proximity"));
        }

        /// <summary>
        /// Ближний план рисуется поверх дальнего со своей глубиной. Солнце светит и ему:
        /// станция освещена тем же светом, что и луна за ней.
        /// </summary>
        void CreateProximity(int layer)
        {
            proximityRoot = new GameObject("Proximity") { layer = layer }.transform;
            proximityRoot.SetParent(transform, false);
            GameObject camObject = new("ProximityCamera") { layer = layer };
            camObject.transform.SetParent(proximityRoot, false);
            proximityCam = camObject.AddComponent<Camera>();
            proximityCam.clearFlags = CameraClearFlags.Depth;
            proximityCam.cullingMask = 1 << layer;
            proximityCam.nearClipPlane = ProximityNear;
            proximityCam.farClipPlane = ProximityFar;
            if (sun != null) sun.cullingMask |= 1 << layer;
        }

        /// <summary>
        /// Камера создаётся кодом, как у навигационного прибора: её положение и поворот ведёт
        /// компонент, а ближняя и дальняя плоскости — условия масштаба, а не вкус.
        /// </summary>
        void CreateCamera(int layer)
        {
            GameObject camObject = new("ExteriorCamera") { layer = layer };
            camObject.transform.SetParent(root, false);
            cam = camObject.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 1 << layer;
            cam.nearClipPlane = Near;
            cam.farClipPlane = Far;
            if (skyMaterial == null) return;
            // Небо — отдельной камерой под внешней. Skybox рисуется после тел с проверкой
            // глубины, а при таком разбросе near/far далёкие тела по глубине неотличимы от неба.
            GameObject skyObject = new("SkyCamera") { layer = layer };
            skyObject.transform.SetParent(root, false);
            skyCam = skyObject.AddComponent<Camera>();
            skyCam.clearFlags = CameraClearFlags.Skybox;
            skyCam.cullingMask = 0;
            sky = new Material(skyMaterial);
            skyObject.AddComponent<Skybox>().material = sky;
            cam.clearFlags = CameraClearFlags.Depth;
        }

        /// <summary>
        /// Камера стыковки: стоит в активном узле корабля и смотрит по его оси. Видит только
        /// ближний план и небо — к узлу подходят с сотен метров, и дальние тела в её кадре
        /// ничего не решают.
        /// </summary>
        public Camera CreateDockingCamera(RenderTexture target, float fieldOfView)
        {
            GameObject camObject = new("DockingCamera") { layer = proximityRoot.gameObject.layer };
            camObject.transform.SetParent(proximityRoot, false);
            dockingCam = camObject.AddComponent<Camera>();
            dockingCam.clearFlags = sky != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            dockingCam.backgroundColor = Color.black;
            if (sky != null) camObject.AddComponent<Skybox>().material = sky;
            dockingCam.cullingMask = proximityCam.cullingMask;
            dockingCam.nearClipPlane = ProximityNear;
            dockingCam.farClipPlane = ProximityFar;
            dockingCam.fieldOfView = fieldOfView;
            dockingCam.targetTexture = target;
            return dockingCam;
        }

        /// <summary>Точка симуляции в мировых координатах сцены ближнего плана.</summary>
        public Vector3 ProximityPoint(Vector3d global)
        {
            Ship ship = (Ship)SimMono.playerShip;
            Vector3d local = Quaterniond.Inverse(ship.attitude.rotation) * (global - ship.simTransform.GLOBAL_R);
            return proximityRoot.TransformPoint(ToHull(local));
        }

        // Тела заводятся в первом кадре, а не в Start: SimMono строит систему в своём Start,
        // и порядок двух Start между объектами не задан.
        void CreateViews()
        {
            List<ObjectData> objects = GameMono.instance.gameData.system.objects;
            views.Add((SimMono.root, CreateView(SimMono.root), objects.Find(o => o.name == SimMono.root.GameObject.name)));
            foreach (SpaceObject body in SimMono.bodies) views.Add((body, CreateView(body), objects.Find(o => o.name == body.GameObject.name)));
            List<StationData> stationData = GameMono.instance.gameData.system.stations;
            // Станция стоит под Солнцем и уходит в тень тел — в отличие от кабины, у которой
            // те же материалы, но свой свет.
            MaterialPropertyBlock eclipse = new();
            eclipse.SetFloat(EclipseId, 1f);
            foreach (Station station in SimMono.stations)
            {
                // Модель у станции обязательна: с неё загрузчик снял узел.
                GameObject view = Instantiate(stationData.Find(s => s.name == station.GameObject.name).view, proximityRoot, false);
                view.name = station.GameObject.name;
                foreach (Transform part in view.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = proximityRoot.gameObject.layer;
                foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>(true)) renderer.SetPropertyBlock(eclipse);
                stationViews.Add((station, view.transform));
            }
        }

        Transform CreateView(SpaceObject body)
        {
            GameObject view = Instantiate(bodyTemplate, root, false);
            view.name = body.GameObject.name;
            foreach (Transform part in view.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = root.gameObject.layer;
            MoonStyle moonStyle = Array.Find(moonStyles, s => s.body == view.name);
            Material style = body.IsRoot ? saturnMaterial :
                view.name == "Titan" ? titanMaterial : moonStyle?.material ?? moonMaterial;
            if (style != null)
            {
                foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = style;
            }
            if (view.name == "Titan")
            {
                titanView = view.transform;
                titanRenderer = view.GetComponentInChildren<Renderer>();
                titanProperties = new MaterialPropertyBlock();
            }
            if (moonStyle != null)
            {
                moons.Add((body, view.GetComponentInChildren<Renderer>()));
                moonProperties ??= new MaterialPropertyBlock();
            }
            if (body.IsRoot) StylizedRing.Create(view.transform, (float)(body.radius / 1000.0), rings);
            return view.transform;
        }

        void LateUpdate()
        {
            if (SimMono.playerShip is not Ship ship) return;
            if (views.Count == 0) CreateViews();
            if (dockingCam != null)
            {
                PortData port = ship.ActivePort;
                dockingCam.transform.SetLocalPositionAndRotation(ToHull(port.position),
                    Quaternion.LookRotation(ToHull(port.axis), ToHull(port.up)));
            }

            FreeCamera free = FreeCamera.instance != null && FreeCamera.instance.Active ? FreeCamera.instance : null;
            Camera eye = free != null ? free.Eye : ActiveEye();
            if (eye == null)
            {
                cam.enabled = false;
                proximityCam.enabled = false;
                if (skyCam != null) skyCam.enabled = false;
                return;
            }
            Follow(eye);

            // Свободная камера смотрит из своей точки и в осях симуляции, а не корабля.
            Quaternion frame = free != null ? Quaternion.identity : hull.rotation;
            root.SetPositionAndRotation(Vector3.zero, frame);
            Quaterniond toShip = free != null ? Quaterniond.identity : Quaterniond.Inverse(ship.attitude.rotation);
            Vector3d origin = free != null ? free.Position : ship.simTransform.GLOBAL_R;
            Quaternion bodyRotation = SimAxes(toShip);
            double epoch = GameMono.instance.Epoch;
            foreach ((SpaceObject body, Transform view, ObjectData data) in views)
            {
                view.localRotation = Spin(body, data, toShip, bodyRotation, epoch);
                view.localPosition = ToHull(toShip * (body.simTransform.GLOBAL_R - origin) / Scale);
                view.localScale = Vector3.one * (float)(2.0 * body.radius / Scale);
            }
            proximityRoot.SetPositionAndRotation(Vector3.zero, frame);
            foreach ((Station station, Transform view) in stationViews)
            {
                // Станция не вращается: её оси — оси симуляции, как у тел.
                view.localRotation = bodyRotation;
                view.localPosition = ToHull(toShip * (station.simTransform.GLOBAL_R - origin));
            }
            if (titanRenderer != null && titanView != null)
            {
                Vector3 toSaturn = views[0].view.localPosition - titanView.localPosition;
                titanRenderer.GetPropertyBlock(titanProperties);
                titanFields.Apply(titanRenderer.sharedMaterial, titanProperties);
                if (toSaturn.sqrMagnitude > 0f)
                    titanProperties.SetVector(SaturnDirectionId, root.TransformDirection(toSaturn.normalized));
                titanRenderer.SetPropertyBlock(titanProperties);
            }
            // Рисунок захваченной луны держится за направление на Сатурн, а шейдеру нужно ещё и
            // направление движения по орбите.
            foreach ((SpaceObject body, Renderer renderer) in moons)
            {
                Vector3 leading = ToHull(toShip * (body.simTransform.GLOBAL_V - SimMono.root.simTransform.GLOBAL_V));
                renderer.GetPropertyBlock(moonProperties);
                moonProperties.SetVector(LeadingId, root.rotation * leading.normalized);
                renderer.SetPropertyBlock(moonProperties);
            }
            SystemData system = GameMono.instance.gameData.system;
            Vector3 toSun = ToHull(toShip * system.sunDirection);
            if (sun != null) sun.transform.rotation = root.rotation * Quaternion.LookRotation(-toSun);
            UpdateEclipse(toShip, origin, root.rotation * toSun.normalized, system);
            if (sky != null)
            {
                // Звёзды неподвижны в инерциальных осях симуляции, поэтому небу передаются
                // эти оси в мировых координатах, а не поворот корпуса.
                sky.SetVector(SunDirectionId, root.rotation * toSun.normalized);
                sky.SetFloat(SunAngularRadiusId, (float)(SunRadius / system.sunDistance));
                sky.SetVector(SkyXId, root.rotation * ToHull(toShip * Vector3d.right));
                sky.SetVector(SkyYId, root.rotation * ToHull(toShip * Vector3d.up));
                sky.SetVector(SkyZId, root.rotation * ToHull(toShip * Vector3d.forward));
            }
        }

        void OnDestroy() => titanFields.Dispose();

        /// <summary>Связанные оси корабля (X вперёд, Y влево, Z вверх, правая) в оси Unity корпуса.</summary>
        /// <summary>
        /// Тела — в метрах, в мировых осях, относительно корабля. Разность считается в double,
        /// во float уходит только она: Титан в миллиарде метров дрожит на десятки метров, а
        /// полутень его тени — тысячи километров.
        /// </summary>
        void UpdateEclipse(Quaterniond toShip, Vector3d origin, Vector3 sunDirection, SystemData system)
        {
            int count = Mathf.Min(views.Count, EclipseMaxBodies);
            for (int i = 0; i < count; i++)
            {
                SpaceObject body = views[i].body;
                Vector3 center = root.rotation * ToHull(toShip * (body.simTransform.GLOBAL_R - origin));
                eclipseBodies[i] = new Vector4(center.x, center.y, center.z, (float)body.radius);
            }
            Shader.SetGlobalVectorArray(EclipseBodiesId, eclipseBodies);
            Shader.SetGlobalFloat(EclipseBodyCountId, count);
            Shader.SetGlobalVector(EclipseSunDirectionId, sunDirection);
            Shader.SetGlobalFloat(EclipseSunRadiusId, (float)(SunRadius / system.sunDistance));
            Shader.SetGlobalFloat(EclipseExteriorScaleId, (float)Scale);
        }

        /// <summary>
        /// Поворот тела в осях корпуса. Захваченное смотрит на центральное тело, ось — нормаль
        /// орбиты: так один оборот приходится ровно на виток, и у наклонной орбиты Япета ось
        /// наклонена вместе с ней. Остальные вращаются вокруг полюса Сатурна — локальной оси Y.
        /// </summary>
        static Quaternion Spin(SpaceObject body, ObjectData data, Quaterniond toShip, Quaternion fixedRotation, double epoch)
        {
            if (data.tidallyLocked)
            {
                Vector3d r = body.simTransform.RELATIVE_R;
                Vector3d v = body.simTransform.RELATIVE_V;
                return Quaternion.LookRotation(ToHull(toShip * -r), ToHull(toShip * Vector3d.Cross(r, v)));
            }
            if (data.rotationPeriod == 0.0) return fixedRotation;
            // Доля оборота — в double: эпоха в миллионах секунд, а угол нужен точнее градуса.
            float angle = (float)(360.0 * (epoch / data.rotationPeriod % 1.0));
            return fixedRotation * Quaternion.AngleAxis(angle, Vector3.up);
        }

        /// <summary>
        /// Оси симуляции в осях сцены. Локальная ось Y тел и станций — полюс Сатурна (ось Z
        /// симуляции, плоскость его экватора — опорная для орбит), поэтому кольца лежат в
        /// экваторе и не крутятся вслед за кораблём.
        /// </summary>
        public static Quaternion SimAxes(Quaterniond toShip) =>
            Quaternion.LookRotation(ToHull(toShip * Vector3d.right), ToHull(toShip * Vector3d.forward));

        /// <summary>Направление сцены обратно в оси симуляции.</summary>
        public static Vector3d FromHull(Vector3 v) => new(v.z, -v.x, v.y);

        public static Vector3 ToHull(Vector3d v) => new((float)-v.y, (float)v.z, (float)v.x);

        static Camera ActiveEye()
        {
            if (PilotMono.instance != null && PilotMono.instance.eye != null && PilotMono.instance.eye.enabled) return PilotMono.instance.eye;
            if (PlayerMono.instance != null && PlayerMono.instance.eye != null && PlayerMono.instance.eye.enabled) return PlayerMono.instance.eye;
            return null;
        }

        /// <summary>
        /// Внешняя камера смотрит туда же, куда глаз, но из нуля: сдвиг головы по кабине —
        /// метры, а внешняя сцена в километрах, параллакса не видно. Космос — фон любого глаза,
        /// поэтому глаз не заливает кадр, а только сбрасывает глубину.
        /// </summary>
        void Follow(Camera eye)
        {
            cam.enabled = true;
            cam.transform.rotation = eye.transform.rotation;
            cam.fieldOfView = eye.fieldOfView;
            cam.depth = eye.depth - 1f;
            proximityCam.enabled = true;
            proximityCam.transform.rotation = eye.transform.rotation;
            proximityCam.fieldOfView = eye.fieldOfView;
            proximityCam.depth = eye.depth - 0.5f;
            if (skyCam != null)
            {
                skyCam.enabled = true;
                skyCam.transform.rotation = eye.transform.rotation;
                skyCam.fieldOfView = eye.fieldOfView;
                skyCam.depth = eye.depth - 2f;
            }
            eye.clearFlags = CameraClearFlags.Depth;
            eye.cullingMask &= ~(cam.cullingMask | proximityCam.cullingMask);
        }
    }
}
