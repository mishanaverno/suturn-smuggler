using Game;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Стыковочные узлы корабля — объекты в сцене кабины, как узлы станции в её модели:
    /// положение — точка узла, синяя ось — наружу из узла, зелёная — его верх. Узел ставят
    /// руками там, где он на корпусе, и отсюда же его видит камера стыковки.
    /// </summary>
    public class ShipPorts : MonoBehaviour
    {
        [Tooltip("Оси корабля в интерьере — те же, что у ExteriorView: forward — продольная ось, up — над головой пилота.")]
        public Transform hull;
        public Transform left;
        public Transform right;

        void Awake()
        {
            if (hull != null && left != null && right != null) return;
            Debug.LogError($"ShipPorts на «{name}»: нужны hull и оба узла — стыковаться нечем.", this);
            enabled = false;
        }

        // Корабль SimMono строит в своём Awake.
        void Start()
        {
            Ship ship = (Ship)SimMono.playerShip;
            ship.leftPort = Port(left);
            ship.rightPort = Port(right);
        }

        /// <summary>Узел в осях корпуса, в метрах: кабина в мировых метрах, масштаб корпуса не в счёт.</summary>
        PortData Port(Transform port)
        {
            Quaternion toHull = Quaternion.Inverse(hull.rotation);
            return SystemLoader.PortFrom(toHull * (port.position - hull.position), toHull * port.forward, toHull * port.up);
        }
    }
}
