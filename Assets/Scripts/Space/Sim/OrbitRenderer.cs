
using System;
using UnityEngine;
using DoublePrecision;
using static UnityEngine.XR.XRDisplaySubsystem;
using NUnit.Framework;
using System.Collections.Generic;
using OuterSpace.Sim;
using System.Runtime.CompilerServices;

namespace OuterSpace.Sim
{
    [RequireComponent(typeof(LineRenderer))]
    public class OrbitRenderer : MonoBehaviour
    {
        public double stepAngleDegrees = 10;
        const int NumPointsEllipse = 360;
        const int NumPointsHyperbola = 100;
        public IHasOrbit parent = null;
        public bool rendered = false;

        private LineRenderer lineRenderer;
        // Start is called before the first frame update
        void Start()
        {
            parent = GetComponentInParent<IHasOrbit>();
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.enabled = false;
            lineRenderer.useWorldSpace = true;
        }
        // LateUpdate, а не FixedUpdate: порядок с SimMono.FixedUpdate не определён,
        // и после смены центрального тела линия отрисовалась бы вокруг старого центра.
        void LateUpdate()
        {
            
            if (parent != null && parent.OrbitParams != null)
            {
                if (!lineRenderer.enabled) lineRenderer.enabled = true;
                Vector3[] positions = GetOrbitPoints(parent.OrbitParams);
                lineRenderer.positionCount = positions.Length;
                lineRenderer.SetPositions(positions);
                //DrawOrbit(parent.OrbitParams, parent.CenterPosition);
            } else
            {
                lineRenderer.enabled = false;
            }
        }
        // Рисует орбиту, возвращает массив точек в мировых координатах (XY плоскость, можно адаптировать)
        // elements.semiMajorAxis - в метрах
        // inclination, longitudeOfAscendingNode, argumentOfPeriapsis - в градусах
        public Vector3[] GetOrbitPoints(OrbitElements elements)
        {
            bool isEllipse = elements.eccentricity < 1.0;
            bool isParabola = Mathf.Abs((float)elements.eccentricity - 1.0f) < 1e-6;
            bool isHyperbola = elements.eccentricity > 1.0;

            int numPoints = isEllipse ? NumPointsEllipse : NumPointsHyperbola;
            Vector3[] points = new Vector3[numPoints];

            double e = elements.eccentricity;
            double a = elements.semiMajorAxis;

            double cosOmega = Mathf.Cos(Mathf.Deg2Rad * (float)elements.longitudeOfAscendingNode);
            double sinOmega = Mathf.Sin(Mathf.Deg2Rad * (float)elements.longitudeOfAscendingNode);
            double cosi = Mathf.Cos(Mathf.Deg2Rad * (float)elements.inclination);
            double sini = Mathf.Sin(Mathf.Deg2Rad * (float)elements.inclination);
            double cosw = Mathf.Cos(Mathf.Deg2Rad * (float)elements.argumentOfPeriapsis);
            double sinw = Mathf.Sin(Mathf.Deg2Rad * (float)elements.argumentOfPeriapsis);
            

            // Функция преобразования из орбитальной плоскости в мировой базис
            System.Func<double, Vector3> OrbitalToWorld = (double trueAnomaly) =>
            {
                // Радиус по формуле орбиты
                double r = 0;
                if (isEllipse)
                {
                    r = a * (1 - e * e) / (1 + e * System.Math.Cos(trueAnomaly));
                }
                else if (isHyperbola)
                {
                    // Для гиперболы semi-major axis < 0, поэтому используем |a|
                    r = System.Math.Abs(a) * (e * e - 1) / (1 + e * System.Math.Cos(trueAnomaly));
                }
                else if (isParabola)
                {
                    // Для параболы используем формулу с фокусным параметром p = 2q (q - перицентр)
                    double q = a * (1 - e); // перицентр (для параболы a ~ inf, тут а можно считать как перицентр)
                    r = 2 * q / (1 + System.Math.Cos(trueAnomaly));
                }

                // Координаты в орбитальной плоскости (X - направлена к перицентру)
                double xOrb = r * System.Math.Cos(trueAnomaly);
                double yOrb = r * System.Math.Sin(trueAnomaly);

                // Повороты: аргумент перицентра w, наклон i, долгота восходящего узла Ω
                double x = (cosw * cosOmega - sinw * sinOmega * cosi) * xOrb +
                           (-sinw * cosOmega - cosw * sinOmega * cosi) * yOrb;
                double y = (cosw * sinOmega + sinw * cosOmega * cosi) * xOrb +
                           (-sinw * sinOmega + cosw * cosOmega * cosi) * yOrb;
                double z = (sinw * sini) * xOrb + (cosw * sini) * yOrb;

                return new Vector3(
                    (float)((x + parent.CenterPosition.x) / Constants.simDistanceMultiplier),
                    (float)((y + parent.CenterPosition.y) / Constants.simDistanceMultiplier),
                    (float)((z + parent.CenterPosition.z) / Constants.simDistanceMultiplier)
                 ); // Y-Z swap, чтобы Z была "вверх"
            };

            if (isEllipse)
            {
                // Эллиптическая орбита - trueAnomaly от 0 до 2π
                for (int i = 0; i < numPoints; i++)
                {
                    double trueAnomaly = 2.0 * System.Math.PI * i / (numPoints - 1);
                    points[i] = OrbitalToWorld(trueAnomaly);
                }
            }
            else if (isHyperbola)
            {
                // Для гиперболы ограничим trueAnomaly углом асимптоты: nu_max = arccos(-1/e)
                double nuMax = System.Math.Acos(-1.0 / e);

                // Рисуем от -nuMax + небольшой запас до +nuMax - запас
                double margin = 0.05; // небольшой отступ, чтобы не рисовать бесконечность
                double startNu = -nuMax + margin;
                double endNu = nuMax - margin;

                for (int i = 0; i < numPoints; i++)
                {
                    double trueAnomaly = startNu + (endNu - startNu) * i / (numPoints - 1);
                    points[i] = OrbitalToWorld(trueAnomaly);
                }
            }
            else if (isParabola)
            {
                // Для параболы можно взять диапазон -pi..pi (около 180 градусов) с отступом
                double margin = 0.05;
                double startNu = -System.Math.PI + margin;
                double endNu = System.Math.PI - margin;

                for (int i = 0; i < numPoints; i++)
                {
                    double trueAnomaly = startNu + (endNu - startNu) * i / (numPoints - 1);
                    points[i] = OrbitalToWorld(trueAnomaly);
                }
            }

            return points;
        }
        public void DrawOrbit(OrbitElements orbitParams, Vector3d centerPosition)
        {
            lineRenderer.enabled = true;
            double points = 360 / stepAngleDegrees;
            List<Vector3> orbitPositions = new();

            Quaterniond rotation_Omega = Quaterniond.Euler(0, 0, orbitParams.longitudeOfAscendingNode);
            Quaterniond rotation_i = Quaterniond.Euler(orbitParams.inclination, 0, 0);
            Quaterniond rotation_omega = Quaterniond.Euler(0, 0, orbitParams.argumentOfPeriapsis);

            Quaterniond rotation = rotation_Omega * rotation_i * rotation_omega;

            for (int j = 0; j <= points; j++)
            {
                double angle = ((j * stepAngleDegrees) + orbitParams.meanAnomalyAtEpoch) * Mathd.Deg2Rad;
                double r = (((orbitParams.semiMajorAxis * (1 - orbitParams.eccentricity * orbitParams.eccentricity)) / (1.0 + orbitParams.eccentricity * Mathd.Cos(angle))));
                // Вычисляем позицию на плоской орбите
                Vector3d orbitPosition_d = new(
                    r * Mathd.Cos(angle),
                    r * Mathd.Sin(angle),
                    0
                );

                // Применяем вращение к локальной позиции
                Vector3d rotatedPosition = rotation * orbitPosition_d;

                // Преобразуем локальные координаты в мировые, добавляя позицию центрального тела
                Vector3d vector = (rotatedPosition / Constants.simDistanceMultiplier) + (centerPosition / Constants.simDistanceMultiplier);
                orbitPositions.Add(new Vector3((float)vector.x, (float)vector.y, (float)vector.z));

            }

            lineRenderer.positionCount = orbitPositions.Count;
            lineRenderer.SetPositions(orbitPositions.ToArray());
            rendered = true;
        }

    }
}