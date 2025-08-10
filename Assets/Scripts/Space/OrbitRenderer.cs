
using System;
using UnityEngine;

namespace OuterSpace
{
    [RequireComponent(typeof(LineRenderer))]
    public class OrbitRenderer : MonoBehaviour
    {
        public ICentralBody centralBody;
        public int orbitPoints = 100;
        public float orbitResolution = 10f; // Шаг истинной аномалии для отрисовки

        private LineRenderer lineRenderer;
        private PlanetMono planet;
        private StarMono star;
        private Boolean needRender = true;
        // Start is called before the first frame update
        void Start()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.positionCount = orbitPoints;
            lineRenderer.useWorldSpace = true;
            planet = GetComponent<PlanetMono>();
            if (planet == null || !planet.enabled ) { planet = GetComponent<SystemBuilder.SBPlanetMono>(); }
            star = GetComponentInParent<StarMono>();
        }

        // Update is called once per frame
        void FixedUpdate()
        {
            if (needRender)
            {
                if (planet.spaceObject.orbitParams != null && planet.spaceObject.orbitParams.eccentricity < 1.0f)
                {
                    if (!lineRenderer.enabled) lineRenderer.enabled = true;
                    //DrawOrbit(star.transform.position, planet.spaceObject.orbitParams);
                    needRender = false;
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

            // Создаем матрицу поворота из углов Эйлера
            Quaternion rotation = Quaternion.Euler(
                0,
                (float)orbitParams.longitudeOfAscendingNode,
                0
            ) * Quaternion.Euler(
                (float)orbitParams.inclination,
                0,
                0
            ) * Quaternion.Euler(
                0,
                0,
                (float)orbitParams.argumentOfPericenter
            );

            for (int j = 0; j <= orbitPoints; j++)
            {
                double angle = (double)j / orbitPoints * 2.0 * Mathd.PI;
                double r = (orbitParams.semiMajorAxis * (1.0 - orbitParams.eccentricity * orbitParams.eccentricity)) / (1.0 + orbitParams.eccentricity * Mathd.Cos(angle));

                Vector3d orbitPosition = new Vector3d(
                    r * Mathd.Cos(angle),
                    r * Mathd.Sin(angle),
                    0
                );

                Vector3 rotatedPosition = rotation * (Vector3)orbitPosition;
                orbitPositions[j] = centerPosition + rotatedPosition;
            }
            lineRenderer.positionCount = orbitPositions.Length;
            lineRenderer.SetPositions(orbitPositions);
        }
    }
}