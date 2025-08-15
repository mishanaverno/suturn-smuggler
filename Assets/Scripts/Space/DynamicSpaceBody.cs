using UnityEngine;
using DoublePrecision;
using OuterSpace.Sim;

namespace OuterSpace
{
    public class SpaceObject
    {
        public Vector3d velocity;
        public OrbitParams orbitParams;
        public ICentralBody centralBody;
        public double mass;
        public SimTransform simTransform;

        public SpaceObject(Transform transform, double mass, Vector3d position, Vector3d velocity, ICentralBody centralBody)
        {
            this.centralBody = centralBody;
            this.velocity = velocity;
            this.mass = mass;
            this.simTransform = new(transform, position);
        }

        public void UpdateVelocity(Vector3d position, Vector3d velocity)
        {
            this.velocity = velocity;
            CalculateOrbit(position);
        }
        public void CalculateOrbit(Vector3d position)
        {
            // Использование double для всех расчетов
            double mu = Constanst.realG * centralBody.Mass;

            double r = position.magnitude;
            double v2 = velocity.sqrMagnitude;
            double specificEnergy = v2 / 2.0 - mu / r;
            Vector3d h = Vector3d.Cross(position, velocity);

            // ---  (a) ---
            double a;
            if (Mathd.Abs(specificEnergy) < Constanst.Tolerance)
            {
                a = double.PositiveInfinity; 
            }
            else
            {
                a = -mu / (2.0 * specificEnergy);
            }

            // ---  (e) ---
            Vector3d eVector = (Vector3d.Cross(velocity, h) / mu) - (position / r);
            double e = eVector.magnitude;

            // ---(i) ---
            double i = Mathd.Acos(Mathd.Clamp(h.z / h.magnitude, -1.0, 1.0));

            // ---(Omega) ---
            Vector3d n = Vector3d.Cross(Vector3d.forward, h);
            double Omega, omega;

            if (n.magnitude < Constanst.Tolerance)
            {
                Omega = 0.0;
                omega = 0.0;
            }
            else
            {
                Omega = Mathd.Acos(Mathd.Clamp(n.x / n.magnitude, -1.0, 1.0));
                if (n.y < 0) Omega = 2.0 * Mathd.PI - Omega;

                if (e < Constanst.Tolerance)
                {
                    omega = 0.0;
                }
                else
                {
                    omega = Mathd.Acos(Mathd.Clamp(Vector3d.Dot(n, eVector) / (n.magnitude * e), -1.0, 1.0));
                    if (eVector.z < 0) omega = 2.0 * Mathd.PI - omega;
                }
            }

            // ---(nu) ---
            double nu;
            if (e < Constanst.Tolerance)
            {
                //
                nu = Mathd.Acos(Mathd.Clamp(Vector3d.Dot(n, position) / (n.magnitude * r), -1.0, 1.0));
                if (Vector3d.Dot(position, Vector3d.Cross(n, h)) < 0) nu = 2.0 * Mathd.PI - nu;
            }
            else
            {
                nu = Mathd.Acos(Mathd.Clamp(Vector3d.Dot(eVector, position) / (e * r), -1.0, 1.0));
                if (Vector3d.Dot(position, velocity) < 0) nu = 2.0 * Mathd.PI - nu;
            }

            // Конвертация углов в градусы только при сохранении
            this.orbitParams = new OrbitParams(mu, a, e, i * Mathd.Rad2Deg, Omega * Mathd.Rad2Deg, omega * Mathd.Rad2Deg, nu * Mathd.Rad2Deg, Time.time);
        }
        
        public Vector3d CalculatePositionAtTime(float time)
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

                M = M_0 + meanMotion * (time - orbitParams.startEpoch);

                // --- Итерационное решение уравнения Кеплера для E ---
                E = M; // Начальное приближение
                for (int i = 0; i < 10; i++)
                {
                    double dE = (M - E + orbitParams.eccentricity * Mathd.Sin(E)) / (1.0 - orbitParams.eccentricity * Mathd.Cos(E));
                    E += dE;
                    if (Mathd.Abs(dE) < 1e-9) break; // Условие сходимости
                }

                r = orbitParams.semiMajorAxis * (1.0 - orbitParams.eccentricity * Mathd.Cos(E));
                nu = Mathd.Atan2(Mathd.Sqrt(1.0 - orbitParams.eccentricity * orbitParams.eccentricity) * Mathd.Sin(E), Mathd.Cos(E) - orbitParams.eccentricity);
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
                return Vector3d.zero;
            }
            // --- Преобразование из орбитальной плоскости в инерциальные координаты ---
            Vector3d orbitPosition = new(r * Mathd.Cos(nu),r * Mathd.Sin(nu), 0);

            Quaterniond rotation_Omega = Quaterniond.Euler(0, 0, orbitParams.longitudeOfAscendingNode);
            Quaterniond rotation_i = Quaterniond.Euler(orbitParams.inclination, 0, 0);
            Quaterniond rotation_omega = Quaterniond.Euler(0, 0, orbitParams.argumentOfPericenter);

            Vector3d finalPosition = rotation_Omega * rotation_i * rotation_omega * orbitPosition;
            return finalPosition;
        }
    }
    
}
