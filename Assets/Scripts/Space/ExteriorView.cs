using System.Collections.Generic;
using DoublePrecision;
using Game;
using Interior;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using UnityEngine;

namespace OuterSpace
{
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

        [Tooltip("Оси корабля в интерьере: forward — продольная ось, куда смотрит тяга; up — над головой пилота.")]
        public Transform hull;
        [Tooltip("Образец тела: сфера диаметром 1 с материалом. Масштаб выставляется под размер тела.")]
        public GameObject bodyTemplate;
        [Tooltip("Солнце: направленный свет, который видит только слой Exterior. Поворачивается само.")]
        public Light sun;

        Transform root;
        Camera cam;
        readonly List<(SpaceObject body, Transform view)> views = new();

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
                return;
            }
            Follow(eye);

            root.SetPositionAndRotation(Vector3.zero, hull.rotation);
            Quaterniond toShip = Quaterniond.Inverse(ship.attitude.rotation);
            Vector3d origin = ship.simTransform.GLOBAL_R;
            foreach ((SpaceObject body, Transform view) in views)
            {
                view.localPosition = ToHull(toShip * (body.simTransform.GLOBAL_R - origin) / Scale);
                view.localScale = Vector3.one * (float)(2.0 * body.radius / Scale);
            }
            if (sun != null)
            {
                Vector3 toSun = ToHull(toShip * GameMono.instance.gameData.system.sunDirection);
                sun.transform.rotation = root.rotation * Quaternion.LookRotation(-toSun);
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
            eye.clearFlags = CameraClearFlags.Depth;
            eye.cullingMask &= ~cam.cullingMask;
        }
    }
}
