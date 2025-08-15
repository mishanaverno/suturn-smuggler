
using System;
using UnityEngine;
using DoublePrecision;
using static UnityEngine.XR.XRDisplaySubsystem;
using NUnit.Framework;
using System.Collections.Generic;

namespace OuterSpace
{
    public class OrbitRenderer : MonoBehaviour
    {
        public ICentralBody centralBody;
        public double stepAngleDegrees = 10;
        public int maxOrbitPoints = 100;

        private LineRenderer lineRenderer;
        private PlanetMono planet;
        // Start is called before the first frame update
        void Start()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            planet = GetComponentInParent<PlanetMono>();
        }

        // Update is called once per frame
        void FixedUpdate()
        {

            if (planet.spaceObject.orbitParams != null && planet.spaceObject.orbitParams.eccentricity < 1.0f)
            {
                if (!lineRenderer.enabled) lineRenderer.enabled = true;
                DrawOrbit(planet.spaceObject.orbitParams);
            }
            else
            {
                // Скрываем линию, если орбита параболическая или гиперболическая
                lineRenderer.enabled = false;
            }
        }
        public void DrawOrbit(OrbitParams orbitParams)
        {
            List<Vector3> orbitPositions = new();

            Quaterniond rotation_Omega = Quaterniond.Euler(0, 0, orbitParams.longitudeOfAscendingNode);
            Quaterniond rotation_i = Quaterniond.Euler(orbitParams.inclination, 0, 0);
            Quaterniond rotation_omega = Quaterniond.Euler(0, 0, orbitParams.argumentOfPericenter);

            Quaterniond rotation = rotation_Omega * rotation_i * rotation_omega;

            for (int j = 0; j <= maxOrbitPoints; j++)
            {
                double angle = (((double)j * stepAngleDegrees) + orbitParams.trueAnomaly) * Mathd.Deg2Rad;
                double r = (((orbitParams.semiMajorAxis * (1 - orbitParams.eccentricity * orbitParams.eccentricity)) / (1.0 + orbitParams.eccentricity * Mathd.Cos(angle))));
                // Вычисляем позицию на плоской орбите
                Vector3d orbitPosition_d = new(
                    r * Mathd.Cos(angle),
                    r * Mathd.Sin(angle),
                    0
                );

                // Применяем вращение к локальной позиции
                Vector3d rotatedPosition = rotation * orbitPosition_d;
                Debug.Log("r mag" + (rotatedPosition.magnitude / Constanst.simDistanceMultiplier));

                // Преобразуем локальные координаты в мировые, добавляя позицию центрального тела
                Vector3d vector = (rotatedPosition / Constanst.simDistanceMultiplier) + (planet.spaceObject.centralBody.SimTransform.Position / Constanst.simDistanceMultiplier) ;
                orbitPositions.Add(new Vector3((float)vector.x, (float)vector.y, (float)vector.z));
                
            }

            lineRenderer.positionCount = orbitPositions.Count;
            lineRenderer.SetPositions(orbitPositions.ToArray());
        }
        
    }
}