using UnityEngine;

namespace OuterSpace.Sim
{
    [RequireComponent(typeof(LineRenderer))]
    public class SOIRenderer : MonoBehaviour
    {
        // Компонент, содержащий данные о SOI
        private IHasSOI parent;
        public bool rendered = false;
        private LineRenderer lineRenderer;
        public int segments = 36; // Количество сегментов для круга

        void Start()
        {
            parent = GetComponentInParent<IHasSOI>();
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.enabled = false;
            lineRenderer.useWorldSpace = true; // Рисуем в локальных координатах объекта
        }

        void Update()
        {
            if (parent != null)
            {
                if (parent.SOI > 0 && parent.SOI < double.PositiveInfinity)
                {
                    DrawSOI();
                }
            }
        }

        private void DrawSOI()
        {
            lineRenderer.positionCount = segments + 1;
            lineRenderer.enabled = true;

            // Получаем радиус SOI из компонента CelestialBody
            float radius = (float)(parent.SOI / Constants.simDistanceMultiplier);
            Vector3 center = new(
                (float)(parent.GlobalPosition.x / Constants.simDistanceMultiplier),
                (float)(parent.GlobalPosition.y / Constants.simDistanceMultiplier),
                (float)(parent.GlobalPosition.z / Constants.simDistanceMultiplier)
            );
            // Рисуем круг в локальных координатах объекта
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / (float)segments * 360f * Mathf.Deg2Rad;
                float x = Mathf.Sin(angle) * radius;
                float y = Mathf.Cos(angle) * radius;
                
                lineRenderer.SetPosition(i, new Vector3(x, 0, y) + center);
            }
            rendered = true;
        }
    }
}
