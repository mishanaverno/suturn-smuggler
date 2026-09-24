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
        [Tooltip("Повёрнута к Сатурну одной стороной. У Гипериона и Фебы вращение своё.")]
        public bool tidallyLocked = true;
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
        readonly List<(SpaceObject body, Transform view)> views = new();
        static readonly int SaturnDirectionId = Shader.PropertyToID("_SaturnDirection");
        Transform titanView;
        Renderer titanRenderer;
        MaterialPropertyBlock titanProperties;
        readonly List<(SpaceObject body, Transform view, Renderer renderer, bool locked)> moons = new();
        MaterialPropertyBlock moonProperties;
        static readonly int LeadingId = Shader.PropertyToID("_Leading");
        // Копия skyMaterial: направления меняются каждый кадр и не должны писаться в ассет.
        Material sky;
        static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");
        static readonly int SunAngularRadiusId = Shader.PropertyToID("_SunAngularRadius");
        static readonly int SkyXId = Shader.PropertyToID("_SkyX");
        static readonly int SkyYId = Shader.PropertyToID("_SkyY");
        static readonly int SkyZId = Shader.PropertyToID("_SkyZ");

        void Awake()
        {
            if (hull == null || bodyTemplate == null || LayerMask.NameToLayer("Exterior") < 0)
            {
                Debug.LogError($"{GetType().Name} на «{name}»: нужны hull, bodyTemplate и слой Exterior.", this);
                enabled = false;
                return;
            }
            int layer = LayerMask.NameToLayer("Exterior");
            root = new GameObject("Exterior") { layer = layer }.transform;
            root.SetParent(transform, false);
            CreateCamera(layer);
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

        // Тела заводятся в первом кадре, а не в Start: SimMono строит систему в своём Start,
        // и порядок двух Start между объектами не задан.
        void CreateViews()
        {
            views.Add((SimMono.root, CreateView(SimMono.root)));
            foreach (SpaceObject body in SimMono.bodies) views.Add((body, CreateView(body)));
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
                moons.Add((body, view.transform, view.GetComponentInChildren<Renderer>(), moonStyle.tidallyLocked));
                moonProperties ??= new MaterialPropertyBlock();
            }
            if (body.IsRoot) StylizedRing.Create(view.transform, (float)(body.radius / 1000.0), rings);
            return view.transform;
        }

        void LateUpdate()
        {
            if (SimMono.playerShip is not Ship ship) return;
            if (views.Count == 0) CreateViews();

            Camera eye = ActiveEye();
            if (eye == null)
            {
                cam.enabled = false;
                if (skyCam != null) skyCam.enabled = false;
                return;
            }
            Follow(eye);

            root.SetPositionAndRotation(Vector3.zero, hull.rotation);
            Quaterniond toShip = Quaterniond.Inverse(ship.attitude.rotation);
            Vector3d origin = ship.simTransform.GLOBAL_R;
            // Локальная ось Y тел — полюс Сатурна (ось Z симуляции, плоскость его экватора —
            // опорная для орбит), поэтому кольца лежат в экваторе и не крутятся вслед за кораблём.
            Vector3 pole = ToHull(toShip * Vector3d.forward);
            Quaternion bodyRotation = Quaternion.LookRotation(ToHull(toShip * Vector3d.right), pole);
            foreach ((SpaceObject body, Transform view) in views)
            {
                view.localRotation = bodyRotation;
                view.localPosition = ToHull(toShip * (body.simTransform.GLOBAL_R - origin) / Scale);
                view.localScale = Vector3.one * (float)(2.0 * body.radius / Scale);
            }
            if (titanRenderer != null && titanView != null)
            {
                Vector3 toSaturn = views[0].view.localPosition - titanView.localPosition;
                if (toSaturn.sqrMagnitude > 0f)
                {
                    titanRenderer.GetPropertyBlock(titanProperties);
                    titanProperties.SetVector(SaturnDirectionId, root.TransformDirection(toSaturn.normalized));
                    titanRenderer.SetPropertyBlock(titanProperties);
                }
            }
            // Захваченная луна смотрит на Сатурн одной стороной, поэтому её рисунок держится
            // за направление на Сатурн, а шейдеру нужно ещё и направление движения по орбите.
            Vector3 saturn = views[0].view.localPosition;
            foreach ((SpaceObject body, Transform view, Renderer renderer, bool locked) in moons)
            {
                if (locked) view.localRotation = Quaternion.LookRotation(saturn - view.localPosition, pole);
                Vector3 leading = ToHull(toShip * (body.simTransform.GLOBAL_V - SimMono.root.simTransform.GLOBAL_V));
                renderer.GetPropertyBlock(moonProperties);
                moonProperties.SetVector(LeadingId, root.rotation * leading.normalized);
                renderer.SetPropertyBlock(moonProperties);
            }
            SystemData system = GameMono.instance.gameData.system;
            Vector3 toSun = ToHull(toShip * system.sunDirection);
            if (sun != null) sun.transform.rotation = root.rotation * Quaternion.LookRotation(-toSun);
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

        /// <summary>Связанные оси корабля (X вперёд, Y влево, Z вверх, правая) в оси Unity корпуса.</summary>
        static Vector3 ToHull(Vector3d v) => new((float)-v.y, (float)v.z, (float)v.x);

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
            if (skyCam != null)
            {
                skyCam.enabled = true;
                skyCam.transform.rotation = eye.transform.rotation;
                skyCam.fieldOfView = eye.fieldOfView;
                skyCam.depth = eye.depth - 2f;
            }
            eye.clearFlags = CameraClearFlags.Depth;
            eye.cullingMask &= ~cam.cullingMask;
        }
    }
}
