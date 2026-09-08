using UnityEngine;
using DoublePrecision;

namespace OuterSpace.Sim
{
    [RequireComponent(typeof(LineRenderer))]
    public class OrbitRenderer : MonoBehaviour
    {
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
            // Толщина линии - в долях высоты экрана прибора: иначе на разных дальностях
            // одна и та же орбита была бы то нитью, то бревном.
            lineRenderer.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
        }
        // LateUpdate, а не FixedUpdate: порядок с SimMono.FixedUpdate не определён,
        // и после смены центрального тела линия отрисовалась бы вокруг старого центра.
        void LateUpdate()
        {
            
            if (parent != null && parent.OrbitParams != null && NavDisplayMono.instance != null)
            {
                if (!lineRenderer.enabled) lineRenderer.enabled = true;
                lineRenderer.widthMultiplier = NavDisplayMono.instance.LineSceneWidth;
                Vector3[] positions = GetOrbitPoints(parent.OrbitParams);
                lineRenderer.positionCount = positions.Length;
                lineRenderer.SetPositions(positions);
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

                return SimView.ToScene(new Vector3d(x, y, z) + parent.CenterPosition);
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
    }
}