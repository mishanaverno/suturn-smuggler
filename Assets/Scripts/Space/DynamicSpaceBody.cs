using UnityEngine;

namespace Space
{
    public class DynamicSpaceObject : StaticSpaceObject
    {
        public Vector3d velocity;
        public OrbitParams orbitParams;
        public ICentralBody centralbody;
        public float startTime;

        public DynamicSpaceObject(double mass, ICentralBody centralBody) : base(mass)
        {
            startTime = Time.time;
            this.centralbody = centralBody;
            Debug.Log("DSO instatiated");
        }
        public void UpdateVelocity(Vector3d position, Vector3d velocity)
        {
            this.velocity = velocity;
            CalculateOrbit(position);
            Debug.Log(position);
            Debug.Log(orbitParams.eccentricity);
        }
        public void CalculateOrbit(Vector3d position)
        {
            double mu = Constanst.G * centralbody.Mass;
            // --- Расчет общей энергии и удельного момента импульса ---
            double r = position.magnitude;
            double v2 = velocity.sqrMagnitude;
            double specificEnergy = v2 / 2f - mu / r;
            Vector3d h = Vector3d.Cross(position, velocity);

            // --- Большая полуось (a) ---
            double a = -mu / (2f * specificEnergy);

            // --- Эксцентриситет (e) ---
            Vector3d eVector = (Vector3d.Cross(velocity, h) /mu) - (position / r);
            double e = eVector.magnitude;

            // --- Наклоненеи (i) ---
            double i = Mathd.Acos(Mathd.Clamp(h.z / h.magnitude, -1f, 1f)) * Mathf.Rad2Deg;

            // --- Долгота восходящего узла (Omega) ---
            Vector3d n = Vector3d.Cross(Vector3d.forward, h); // Вектор узла
            double Omega, omega;
            if (n.magnitude < Constanst.Tolerance) // Если орбита в плоскости XY
            {
                Omega = 0f;
                omega = 0f;
            }
            else
            {
                Omega = Mathd.Acos(Mathd.Clamp(n.x / n.magnitude, -1f, 1f)) * Mathf.Rad2Deg;
                if (n.y < 0) Omega = 360f - Omega;

                if (e < Constanst.Tolerance) // Если орбита круговая, но не в плоскости XY
                {
                    omega = 0f;
                }
                else
                {
                    omega = Mathd.Acos(Mathd.Clamp(Vector3d.Dot(n, eVector) / (n.magnitude * e), -1f, 1f)) * Mathd.Rad2Deg;
                    if (eVector.z < 0) omega = 360f - omega;
                }
            }

            // --- Истинная аномалия (nu) ---
            double nu;
            if (e < Constanst.Tolerance)
            {
                // Для круговой орбиты истинная аномалия - это просто угол позиции
                // относительно восходящего узла (или оси X в нашем тестовом случае).
                // rework to Vector3d
                Vector3 positionInPlane = Quaternion.Euler((float)-Omega, (float)-i, (float)-omega) * new Vector3((float)position.x, (float)position.y, (float)position.z);
                nu = Mathf.Atan2(positionInPlane.y, positionInPlane.x) * Mathf.Rad2Deg;
                if (nu < 0) nu += 360f;
            }
            else
            {
                nu = Mathd.Acos(Mathd.Clamp(Vector3d.Dot(eVector, position) / ((float)e * (float)r), -1f, 1f)) * Mathf.Rad2Deg;
                if (Vector3d.Dot(position, velocity) < 0) nu = 360f - nu;
            }
            this.orbitParams = new OrbitParams(mu, a, e, i, Omega, omega, nu);
        }

        public Vector3 CalculatePositionAtTime(float time)
        {
            double E; // Эксцентрическая аномалия
            double nu; // Истинная аномалия
            double r;  // Радиус-вектор
            double M;  // Средняя аномалия

            // --- Расчет средней аномалии (M) на момент времени 'time' ---
            double meanMotion = Mathd.Sqrt(orbitParams.mu / Mathd.Pow(Mathd.Abs(orbitParams.semiMajorAxis), 3));
            // Для эллиптической орбиты
            if (orbitParams.eccentricity < 1.0)
            {
                // Вычисление начальной эксцентрической аномалии из истинной
                double cos_nu_0 = Mathd.Cos(orbitParams.trueAnomaly * Mathd.PI / 180.0);
                double sin_nu_0 = Mathd.Sin(orbitParams.trueAnomaly * Mathd.PI / 180.0);
                double E_0 = Mathd.Atan2(sin_nu_0 * Mathd.Sqrt(1.0 - orbitParams.eccentricity * orbitParams.eccentricity),
                                              orbitParams.eccentricity + cos_nu_0);

                // Начальная средняя аномалия
                double M_0 = E_0 - orbitParams.eccentricity * Mathd.Sin(E_0);

                M = M_0 + meanMotion * (time - startTime);

                // --- Итерационное решение уравнения Кеплера для E ---
                E = M; // Начальное приближение
                for (int i = 0; i < 10; i++)
                {
                    double dE = (M - E + orbitParams.eccentricity * Mathd.Sin(E)) / (1.0 - orbitParams.eccentricity * Mathd.Cos(E));
                    E += dE;
                    if (Mathd.Abs(dE) < 1e-9) break; // Условие сходимости
                }

                r = orbitParams.semiMajorAxis * (1.0 - orbitParams.eccentricity * Mathd.Cos(E));
                nu = Mathd.Atan2(System.Math.Sqrt(1.0 - orbitParams.eccentricity * orbitParams.eccentricity) * Mathd.Sin(E), Mathd.Cos(E) - orbitParams.eccentricity);
            }
            // Для параболической и гиперболической орбит
            else
            {
                // Расчет с использованием гиперболической или параболической аномалии
                // Этот блок кода требует других формул и логики.
                // Например, для гиперболической орбиты используются гиперболические функции.
                // Это выходит за рамки простого исправления, поэтому я оставлю его как заглушку.
                // Реализация будет зависеть от ваших конкретных потребностей.
                // Для гиперболической: H - e * sinh(H) = M;
                // Для параболической: tan(nu/2) + 1/3 * tan(nu/2)^3 = M + c;
                return Vector3.zero;
            }
            // --- Преобразование из орбитальной плоскости в инерциальные координаты ---
            Vector3d orbitPosition = new Vector3d((r * Mathd.Cos(nu)), (float)(r * Mathd.Sin(nu)), 0);

            Quaternion rotation_Omega = Quaternion.Euler(0, 0, (float)orbitParams.longitudeOfAscendingNode);
            Quaternion rotation_i = Quaternion.Euler((float)orbitParams.inclination, 0, 0);
            Quaternion rotation_omega = Quaternion.Euler(0, 0, (float)orbitParams.argumentOfPericenter);

            Vector3 finalPosition = rotation_Omega * rotation_i * rotation_omega * orbitPosition.CastToVector3();

            return finalPosition;
        }
    }
    
}
