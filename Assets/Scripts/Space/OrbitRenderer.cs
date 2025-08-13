
using System;
using UnityEngine;

namespace OuterSpace
{
    public class OrbitRenderer : MonoBehaviour
    {
        public ICentralBody centralBody;
        public int orbitPoints = 100;
        public float orbitResolution = 10f; // Шаг истинной аномалии для отрисовки

        private LineRenderer lineRenderer;
        private PlanetMono planet;
        private Boolean needRender = true;
        // Start is called before the first frame update
        void Start()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.positionCount = orbitPoints;
            lineRenderer.useWorldSpace = true;
            planet = GetComponentInParent<PlanetMono>();
            Debug.Log(planet.name);
            Debug.Log(planet.enabled);
            if (planet == null || !planet.enabled ) { planet = GetComponentInParent<SystemBuilder.SBPlanetMono>(); }
        }

        // Update is called once per frame
        void FixedUpdate()
        {
            if (needRender)
            {
                if (planet.spaceObject.orbitParams != null && planet.spaceObject.orbitParams.eccentricity < 1.0f)
                {
                    if (!lineRenderer.enabled) lineRenderer.enabled = true;
                    DrawOrbit(planet.spaceObject.centralBody.RelativePosition.CastToVector3(), planet.spaceObject.orbitParams);
                    
                }
                else
                {
                    // Скрываем линию, если орбита параболическая или гиперболическая
                    lineRenderer.enabled = false;
                }
            }
        }
        public void DrawOrbit(Vector3 centerPosition, OrbitParams orbitParams)
        {
            Vector3[] orbitPositions = new Vector3[orbitPoints + 1];

            // ПРАВИЛЬНЫЙ ПОРЯДОК: Z(Omega) -> X(i) -> Z(omega)
            // Quaternion.Euler ожидает углы в ГРАДУСАХ.
            // 1. Поворот вокруг Z (Аргумент перицентра)
            // 2. Поворот вокруг X (Наклонение)
            // 3. Поворот вокруг Z (Долгота узла)
 
            Quaternion rotation_Omega = Quaternion.Euler(0, 0, (float)orbitParams.longitudeOfAscendingNode);
            Quaternion rotation_i = Quaternion.Euler((float)orbitParams.inclination, 0, 0);
            Quaternion rotation_omega = Quaternion.Euler(0, 0, (float)orbitParams.argumentOfPericenter);

            Quaternion rotation = rotation_Omega * rotation_i * rotation_omega;

            for (int j = 0; j <= orbitPoints; j++)
            {
                double angle = (double)j / orbitPoints * 2.0 * Mathd.PI;
                double r = (orbitParams.semiMajorAxis * (1.0 - orbitParams.eccentricity * orbitParams.eccentricity)) / (1.0 + orbitParams.eccentricity * Mathd.Cos(angle));

                // Вычисляем позицию на плоской орбите
                Vector3d orbitPosition_d = new(
                    r * Mathd.Cos(angle),
                    r * Mathd.Sin(angle),
                    0
                );

                // Применяем вращение к локальной позиции
                Vector3 rotatedPosition = rotation * (Vector3)orbitPosition_d;

                // Преобразуем локальные координаты в мировые, добавляя позицию центрального тела
                orbitPositions[j] = centerPosition + rotatedPosition;
            }

            lineRenderer.positionCount = orbitPositions.Length;
            lineRenderer.SetPositions(orbitPositions);
        }
    }
}