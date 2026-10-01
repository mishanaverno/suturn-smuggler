using DoublePrecision;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using UnityEngine;

namespace OuterSpace
{
    /// <summary>
    /// Касания корабля со станциями. Геометрию ищет Unity, исход считает симуляция
    /// (Ship.Bump): своего физического движка нет, и всё, что задаёт форму, видно в сцене.
    ///
    /// Форма станции — коллайдеры на её модели, формы корабля — коллайдер в кабине, на
    /// слое Proximity. Коллайдеры никуда не двигаются: Physics.ComputePenetration принимает
    /// положения явно, и обе формы ставятся в оси корпуса — станция туда же, куда её ставит
    /// ExteriorView. Rigidbody нет: Unity спорил бы с Кеплером.
    ///
    /// Тик — после SimMono: корабль и станции уже на этом тике.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class StationContacts : MonoBehaviour
    {
        [Tooltip("Вид наружу: у него модели станций и оси корпуса.")]
        public ExteriorView exterior;
        [Tooltip("Форма корабля: коллайдер в кабине, на слое Proximity. Коробка, сфера, капсула или выпуклая сетка — станция может быть любой.")]
        public Collider ship;

        void Awake()
        {
            if (exterior != null && ship != null)
            {
                // Модели станций и форма корабля стоят в той же сцене, что кабина, и тело
                // игрока упиралось бы в них, а рука — наводилась.
                int proximity = LayerMask.NameToLayer("Proximity");
                for (int layer = 0; layer < 32; layer++)
                    if (layer != proximity) Physics.IgnoreLayerCollision(proximity, layer);
                return;
            }
            Debug.LogError($"StationContacts на «{name}»: нужны exterior и коллайдер корабля.", this);
            enabled = false;
        }

        void FixedUpdate()
        {
            if (SimMono.playerShip is not Ship { DockedTo: null, LandedOn: null, Wreck: null } playerShip) return;

            Transform hull = exterior.hull;
            Quaternion toHull = Quaternion.Inverse(hull.rotation);
            Vector3 shipPosition = toHull * (ship.transform.position - hull.position);
            Quaternion shipRotation = toHull * ship.transform.rotation;
            Quaterniond toShip = Quaterniond.Inverse(playerShip.attitude.rotation);
            Quaternion stationRotation = ExteriorView.SimAxes(toShip);

            foreach ((Station station, Transform view) in exterior.StationViews)
            {
                Vector3d offset = station.simTransform.GLOBAL_R - playerShip.simTransform.GLOBAL_R;
                if (offset.magnitude > station.beacon.range) continue;

                Vector3 stationPosition = ExteriorView.ToHull(toShip * offset);
                Quaternion fromView = Quaternion.Inverse(view.rotation);
                Vector3 push = Vector3.zero;
                float depth = 0f;
                foreach (Collider part in view.GetComponentsInChildren<Collider>())
                {
                    Vector3 position = stationPosition + stationRotation * (fromView * (part.transform.position - view.position));
                    Quaternion rotation = stationRotation * fromView * part.transform.rotation;
                    if (!Physics.ComputePenetration(ship, shipPosition, shipRotation, part, position, rotation,
                            out Vector3 direction, out float distance) || distance <= depth) continue;
                    push = direction;
                    depth = distance;
                }
                if (depth > 0f) playerShip.Bump(station, playerShip.attitude.rotation * ExteriorView.FromHull(push), depth);
            }
        }
    }
}
