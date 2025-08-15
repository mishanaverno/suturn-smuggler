using UnityEngine;

namespace OuterSpace
{
    [RequireComponent(typeof(LineRenderer))]
    public class SOIRenderer : MonoBehaviour
    {
        // Компонент, содержащий данные о SOI
        public PlanetMono spaceObjectMono;

        private LineRenderer lineRenderer;
        public int segments = 36; // Количество сегментов для круга

        void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false; // Рисуем в локальных координатах объекта
            spaceObjectMono = GetComponentInParent<PlanetMono>();
        }

        void Update()
        {
            // Проверяем, был ли радиус SOI уже рассчитан
            if (spaceObjectMono.SOI > 0)
            {
                DrawSOI();
            }
        }

        private void DrawSOI()
        {
            lineRenderer.positionCount = segments + 1;

            // Получаем радиус SOI из компонента CelestialBody
            float radius = (float)(spaceObjectMono.SOI / Constanst.simDistanceMultiplier);

            // Рисуем круг в локальных координатах объекта
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / (float)segments * 360f * Mathf.Deg2Rad;
                float x = Mathf.Sin(angle) * radius;
                float y = Mathf.Cos(angle) * radius;

                lineRenderer.SetPosition(i, new Vector3(x, 0, y));
            }
        }
    }
}
