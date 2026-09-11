
using DoublePrecision;
using Utilities;
using System;
using System.Collections.Generic;

namespace OuterSpace.Sim
{
    public static class AstroDynamic
    {
        /// <summary>
        /// Вычисляет орбитальные элементы по положению и скорости объекта.
        /// </summary>
        /// <param name="position">Положение объекта относительно центрального тела (Vector3d), м</param>
        /// <param name="velocity">Скорость объекта относительно центрального тела (Vector3d), м/с</param>
        /// <param name="mu">Гравитационный параметр (GM) центрального тела, м³/с²</param>
        /// <param name="epoch">Время эпохи (секунды)</param>
        /// <returns>Орбитальные элементы объекта</returns>
        public static OrbitElements CalculateOrbitElements(Vector3d position, Vector3d velocity, double mu, double epoch)
        {
            const double SMALL = 1e-15;
            const double CIRCULAR = 1e-9;
            const double EQUATORIAL_DEG = 1e-9;

            OrbitElements elements = new OrbitElements();
            elements.startEpoch = epoch;
            elements.mu = mu;

            Vector3d r = position;
            Vector3d v = velocity;

            double rMag = r.magnitude;
            double vMag = v.magnitude;

            Vector3d h = Vector3d.Cross(r, v);
            double hMag = h.magnitude;

            Vector3d k = new Vector3d(0, 0, 1);
            // Узловой вектор
            Vector3d n = Vector3d.Cross(k, h);
            double nMag = n.magnitude;

            // Эксцентриситет
            Vector3d eVector = (Vector3d.Cross(v, h) / mu) - (r / rMag);
            double eccentricity = eVector.magnitude;
            if (eccentricity == 1.0) { eccentricity += SMALL; } // парабола не поддерживается решателем Кеплера
            elements.eccentricity = eccentricity;

            // Энергия и полуось
            double energy = vMag * vMag / 2.0 - mu / rMag;
            if (Math.Abs(energy) < SMALL)
                elements.semiMajorAxis = double.PositiveInfinity; // парабола
            else
                elements.semiMajorAxis = -mu / (2.0 * energy);

            // Особый случай: почти нулевой угловой момент
            if (hMag < SMALL)
            {
                // Орбита вырождается в параболическую или прямую линию,
                // наклонение не определено, задаём углы в 0 или NaN по соглашению
                elements.inclination = 0.0;
                elements.longitudeOfAscendingNode = 0.0;
                elements.argumentOfPeriapsis = 0.0;

                // Вычисляем эксцентриситет и полуось
                eVector = (Vector3d.Cross(v, h) / mu) - (r / rMag);
                elements.eccentricity = eVector.magnitude;

                energy = vMag * vMag / 2.0 - mu / rMag;
                if (Math.Abs(energy) < SMALL)
                    elements.semiMajorAxis = double.PositiveInfinity; // Параболическая
                else
                    elements.semiMajorAxis = -mu / (2.0 * energy);

                // Аномалии вычислить не пытаемся, задаём 0
                //elements.trueAnomaly = 0.0;

                return elements;
            }

            // Наклонение
            elements.inclination = (Math.Acos(Mathd.Clamp(h.z / hMag, -1.0, 1.0))) * Mathd.Rad2Deg;

            bool isEquatorial = elements.inclination < EQUATORIAL_DEG || Math.Abs(elements.inclination - 180.0) < EQUATORIAL_DEG;

            // Долгота восходящего узла (Omega)
            if (isEquatorial)
            {
                elements.longitudeOfAscendingNode = 0.0; // орбита экваториальная
            }
            else
            {
                double omega = Math.Acos(Mathd.Clamp(n.x / nMag, -1.0, 1.0));
                if (n.y < 0)
                    omega = 2.0 * Math.PI - omega;
                elements.longitudeOfAscendingNode = omega * Mathd.Rad2Deg;
            }

            // Аргумент перицентра (omega)
            double argPeriapsis;
            bool isCircular = eccentricity < CIRCULAR;

            if (isCircular)
            {
                argPeriapsis = 0.0;
            }
            else if (!isEquatorial)
            {
                argPeriapsis = Math.Acos(Mathd.Clamp(Vector3d.Dot(n, eVector) / (nMag * eccentricity), -1.0, 1.0));
                if (eVector.z < 0)
                    argPeriapsis = 2.0 * Math.PI - argPeriapsis;
            }
            else
            {
                // Для ретроградной экваториальной орбиты (i = 180) перифокальная ось Y смотрит
                // против инерциальной, поэтому признак второй полуплоскости инвертируется.
                argPeriapsis = Math.Acos(Mathd.Clamp(eVector.x / eccentricity, -1.0, 1.0));
                if ((eVector.y < 0) != (h.z < 0))
                    argPeriapsis = 2.0 * Math.PI - argPeriapsis;
            }
            elements.argumentOfPeriapsis = argPeriapsis * Mathd.Rad2Deg;

            // Истинная аномалия (true anomaly)
            double trueAnomaly;
            if (isCircular)
            {
                // Для круговой орбиты истинная аномалия определяется по углу между n и r
                if (isEquatorial)
                {
                    // Орбита экваториальная и круглая, считаем trueAnomaly от оси X
                    trueAnomaly = Math.Acos(Mathd.Clamp(r.x / rMag, -1.0, 1.0));
                    if ((r.y < 0) != (h.z < 0))
                        trueAnomaly = 2.0 * Math.PI - trueAnomaly;
                }
                else
                {
                    trueAnomaly = Math.Acos(Mathd.Clamp(Vector3d.Dot(n, r) / (nMag * rMag), -1.0, 1.0));
                    if (r.z < 0)
                        trueAnomaly = 2.0 * Math.PI - trueAnomaly;
                }
            }
            else
            {
                // Используем косинус для определения угла
                trueAnomaly = Math.Acos(Mathd.Clamp(Vector3d.Dot(eVector, r) / (eccentricity * rMag), -1.0, 1.0));

                // Используем векторное произведение для определения квадранта
                Vector3d cross = Vector3d.Cross(eVector, r);
                if (Vector3d.Dot(cross, h) < 0)
                    trueAnomaly = 2.0 * Math.PI - trueAnomaly;
            }

            // Эксцентрическая аномалия (E)
            double eccentricAnomaly;
            if (isCircular)
            {
                eccentricAnomaly = trueAnomaly;
            }
            else if (eccentricity < 1.0) // Эллиптическая орбита
            {
                double cosE = (eccentricity + Math.Cos(trueAnomaly)) / (1 + eccentricity * Math.Cos(trueAnomaly));
                double sinE = Math.Sqrt(1 - cosE * cosE);
                if (trueAnomaly > Math.PI)
                    sinE = -sinE;
                eccentricAnomaly = Math.Atan2(sinE, cosE);
                if (eccentricAnomaly < 0) eccentricAnomaly += 2.0 * Math.PI;
            }
            else
            {
                eccentricAnomaly = HyperbolicAnomaly(eccentricity, trueAnomaly);
            }

            // Средняя аномалия (M)
            double meanAnomaly;
            if (isCircular)
            {
                meanAnomaly = eccentricAnomaly;
            }
            else if (eccentricity < 1.0)
            {
                meanAnomaly = eccentricAnomaly - eccentricity * Math.Sin(eccentricAnomaly);
                if (meanAnomaly < 0) meanAnomaly += 2.0 * Math.PI;
            }
            else
            {
                meanAnomaly = eccentricity * Math.Sinh(eccentricAnomaly) - eccentricAnomaly;
            }
            elements.meanAnomalyAtEpoch = meanAnomaly;
            return elements;
        }
        /// <summary>
        /// Гиперболическая аномалия H по истинной аномалии.
        /// </summary>
        private static double HyperbolicAnomaly(double e, double nu)
        {
            double tanTerm = Math.Sqrt((e - 1.0) / (e + 1.0)) * Math.Tan(nu / 2.0);
            if (Math.Abs(tanTerm) >= 1.0)
                throw new ArgumentOutOfRangeException(nameof(nu), $"Истинная аномалия {nu} за асимптотой гиперболы e = {e}");
            // Math.Atanh отсутствует в .NET Standard 2.0, на который собирается Unity
            return Math.Log((1.0 + tanTerm) / (1.0 - tanTerm));
        }

        public static (Vector3d r, Vector3d v) CalcRelativePositionAndVelocityAtEpoch(OrbitElements elements, double epoch, bool debug = false)
        {
            double dt = epoch - elements.startEpoch;

            (Vector3d position, Vector3d velocity) = CalculatePerifocalVectors(elements, dt, debug);
            return (RotatePerifocalToInertial(position, elements), RotatePerifocalToInertial(velocity, elements));

        }
        private static (Vector3d r, Vector3d v) CalculatePerifocalVectors(OrbitElements orbitParams, double dt, bool debug = false)
        {
            Vector3d position;
            Vector3d velocity;
            // Perifocal система координат - это орбитальная система координат, связанная с плоскостью орбиты спутника.Ее оси:
            // P - axis: Направлена от центрального тела к перицентру орбиты
            // Q - axis: Перпендикулярна P-axis в плоскости орбиты, на 90° по направлению движения
            // W - axis: Перпендикулярна плоскости орбиты(P × Q)
            Log.Debug($"=== DEBUG ===", debug);
            double mu = orbitParams.mu;
            double e = orbitParams.eccentricity;
            double a = orbitParams.semiMajorAxis;

            (double nu, double E, double H) = GetAnomalyesAtTime(orbitParams, dt, debug); // Надо расширить для всех орбит

            if (e < 1.0)
            {
                // Эллиптическая орбита

                double p = a * (1 - e * e);
                //double r = p / (1 + e * Mathd.Cos(nu));
                double r = a * (1 - e * Mathd.Cos(E));
                double h = Mathd.Sqrt(mu * p);

                position = new Vector3d(
                    r * Mathd.Cos(nu),
                    r * Mathd.Sin(nu),
                    0.0
                );

                double v_r = (mu / h) * e * Mathd.Sin(nu);
                double v_theta = (mu / h) * (1 + e * Mathd.Cos(nu));

                velocity = new Vector3d(
                    v_r * Mathd.Cos(nu) - v_theta * Mathd.Sin(nu),
                    v_r * Mathd.Sin(nu) + v_theta * Mathd.Cos(nu),
                    0.0
                );
                Log.Debug($"p = {p}", debug);
                Log.Debug($"h = {h}", debug);
                Log.Debug($"v_r = {v_r}, v_theta = {v_theta}", debug);
                Log.Debug($"v_x = {velocity.x}, v_y = {velocity.y}, v_z = {velocity.z}", debug);
                Log.Debug($"r_x = {position.x}, r_y = {position.y}, r_z = {position.z}", debug);
            }
            else if (e > 1.0)
            {
                // Гиперболическая орбита

                // Фокальный параметр
                double p = Mathd.Abs(a) * (e * e - 1);

                // Радиус
                double r = p / (1 + e * Math.Cos(nu));

                // Орбитальный момент
                double h = Math.Sqrt(mu * p);

                position = new Vector3d(
                    r * Math.Cos(nu),
                    r * Math.Sin(nu),
                    0.0
                );

                double v_r = (mu / h) * e * Math.Sin(nu);
                double v_theta = (mu / h) * (1 + e * Math.Cos(nu));

                velocity = new Vector3d(
                    v_r * Math.Cos(nu) - v_theta * Math.Sin(nu), 
                    v_r * Math.Sin(nu) + v_theta * Math.Cos(nu),
                    0.0
                );
                Log.Debug($"p = {p}", debug);
                Log.Debug($"r = {r}", debug);
                Log.Debug($"h = {h}", debug);
                Log.Debug($"v_r = {v_r}, v_theta = {v_theta}", debug);
                Log.Debug($"v_x = {velocity.x}, v_y = {velocity.y}, v_z = {velocity.z}", debug);
                Log.Debug($"r_x = {position.x}, r_y = {position.y}, r_z = {position.z}", debug);
            }
            else
            {
                throw new Exception("Некорректные параметры орбиты");
            }
            Log.Debug($"=== END DEBUG ===", debug);
            Log.EndDebug(debug);
            return (position, velocity);
        }

        private static Vector3d RotatePerifocalToInertial(Vector3d vec, OrbitElements orbitParams)
        {
            // Углы орбиты в радианах
            double raan = orbitParams.longitudeOfAscendingNode * Mathd.Deg2Rad;     // Ω
            double inclination = orbitParams.inclination * Mathd.Deg2Rad;           // i
            double argPeriapsis = orbitParams.argumentOfPeriapsis * Mathd.Deg2Rad;  // ω

            // Предвычисления тригонометрии
            double cosRaan = Math.Cos(raan);
            double sinRaan = Math.Sin(raan);
            double cosI = Math.Cos(inclination);
            double sinI = Math.Sin(inclination);
            double cosArgPeriapsis = Math.Cos(argPeriapsis);
            double sinArgPeriapsis = Math.Sin(argPeriapsis);

            // Элементы полной матрицы поворота R = Rz(raan) * Rx(inclination) * Rz(argPeriapsis)
            double R11 = cosRaan * cosArgPeriapsis - sinRaan * sinArgPeriapsis * cosI;
            double R12 = -cosRaan * sinArgPeriapsis - sinRaan * cosArgPeriapsis * cosI;
            double R13 = sinRaan * sinI;

            double R21 = sinRaan * cosArgPeriapsis + cosRaan * sinArgPeriapsis * cosI;
            double R22 = -sinRaan * sinArgPeriapsis + cosRaan * cosArgPeriapsis * cosI;
            double R23 = -cosRaan * sinI;

            double R31 = sinArgPeriapsis * sinI;
            double R32 = cosArgPeriapsis * sinI;
            double R33 = cosI;

            // Применение матрицы к вектору в перефокальной системе (PQW)
            double x = R11 * vec.x + R12 * vec.y + R13 * vec.z;
            double y = R21 * vec.x + R22 * vec.y + R23 * vec.z;
            double z = R31 * vec.x + R32 * vec.y + R33 * vec.z;

            return new Vector3d(x, y, z);
        }
        
        public static (double nu, double E, double H) GetAnomalyesAtTime(OrbitElements orbit, double dt, bool debug = false)
        {
            Log.Debug($"Get TrueAnomaly at {orbit.startEpoch + dt}", debug);

            double a = orbit.semiMajorAxis;
            double e = orbit.eccentricity;
            double nu = 0;
            double M = orbit.meanAnomalyAtEpoch;
            double E = 0;
            double H = 0;

            Log.Debug($"a = {a}, e = {e}, mu = {orbit.mu}", debug);
            Log.Debug($"dt = {dt}", debug);
            Log.Debug($"meanAnomalyAtEpoch = {orbit.meanAnomalyAtEpoch}", debug);

            if (e == 1.0)
                throw new NotSupportedException("Parabolic orbits (e=1) are not supported.");

            else if (e < 1)
            {
                double n = Math.Sqrt(orbit.mu / Math.Pow(a, 3)); // Среднее движение
                M = M + n * dt;

                Log.Debug($"n = {n}", debug);
                Log.Debug($"M = {M}", debug);
                // Нормализуем M в [0, 2π)
                M = M % (2 * Math.PI);
                if (M < 0) M += 2 * Math.PI;

                // Решаем уравнение Кеплера: M = E - e * sin(E).
                // Переводим M в (-pi, pi] — иначе Ньютон стартует далеко от корня и при большом e расходится.
                if (M > Math.PI) M -= 2.0 * Math.PI;
                E = e < 0.8 ? M : Math.PI * Math.Sign(M == 0.0 ? 1.0 : M);
                Log.Debug($"Initial E = {E}", debug);
                for (int i = 0; i < 60; i++)
                {
                    double f = E - e * Math.Sin(E) - M;
                    double df = 1 - e * Math.Cos(E);
                    double dE = -f / df;  // Минус!

                    E += dE;
                    Log.Debug($"Iteration {i}: E = {E}, f = {f}, df = {df}, dE = {dE}", debug);
                    if (Math.Abs(dE) < 1e-14) break;
                }
                Log.Debug($"Final E = {E}", debug);
                // Истинная аномалия
                nu = 2.0 * Math.Atan2(
                    Math.Sqrt(1 + e) * Math.Sin(E / 2),
                    Math.Sqrt(1 - e) * Math.Cos(E / 2)
                );

                // Нормализуем в [0, 2π)
                nu = nu % (2 * Math.PI);
                if (nu < 0) nu += 2 * Math.PI;
                Log.Debug($"nu = {nu} radians", debug);
                Log.Debug($"nu = {nu * 180 / Math.PI} degrees", debug);
            } else
            {
                double n = Math.Sqrt(orbit.mu / Math.Abs(Math.Pow(a, 3))); // Среднее движение (мнимая величина)
                M = M + n * dt;

                Log.Debug($"n = {n}", debug);
                Log.Debug($"M = {M}", debug);

                // Решаем гиперболическое уравнение Кеплера: M = e * sinh(H) - H
                // Начальное приближение H = ln(2M/e + 1.8)
                H = Math.Asinh(M / e);

                for (int i = 0; i < 100; i++)
                {
                    double f = e * Math.Sinh(H) - H - M;
                    double df = e * Math.Cosh(H) - 1;
                    double dH = -f / df;
                    H += dH;
                    Log.Debug($"Iteration {i}: H = {H}, f = {f}, df = {df}, dH = {dH}", debug);
                    if (Math.Abs(dH) < 1e-14) break;
                }
                Log.Debug($"Final H = {H}", debug);

                // Истинная аномалия
                nu = 2 * Math.Atan2(
                    Math.Sqrt(e + 1) * Math.Sinh(H / 2),
                    Math.Sqrt(e - 1) * Math.Cosh(H / 2)
                );

                nu = nu % (2 * Math.PI);
                if (nu < 0) nu += 2 * Math.PI;

                Log.Debug($"nu = {nu} radians", debug);
                Log.Debug($"nu = {nu * 180 / Math.PI} degrees", debug);
            }
            return (nu, E, H); // в радианах
        }
        // Целевой угол между соседними точками дуги, видимый из фокуса. Три градуса — это
        // заведомо кривая, а не ломаная, при любом эксцентриситете и любом масштабе.
        const double SampleAngle = 3.0 * Mathd.Deg2Rad;
        // Гипербола рисуется не до самой асимптоты: там радиус уходит в бесконечность.
        const double AsymptoteMargin = 0.05;
        // Парабола не поддерживается решателем Кеплера. Рисовать её не надо, но и падать
        // из LateUpdate нельзя.
        const double ParabolicTolerance = 1e-9;

        /// <summary>
        /// Точки дуги в системе отсчёта центрального тела за отрезок времени. Замкнутая орбита
        /// рисуется не дольше витка: дальше кривая просто повторяется.
        /// </summary>
        public static void SampleArc(OrbitElements orbit, double fromEpoch, double toEpoch, int maxPoints, List<Vector3d> into)
        {
            into.Clear();
            if (Math.Abs(orbit.eccentricity - 1.0) < ParabolicTolerance) return;

            double span = toEpoch - fromEpoch;
            if (span <= 0.0) return;

            double sweep;
            double nuFrom = GetAnomalyesAtTime(orbit, fromEpoch - orbit.startEpoch).nu;
            if (orbit.eccentricity < 1.0)
            {
                double period = 2.0 * Math.PI * Math.Sqrt(Math.Pow(orbit.semiMajorAxis, 3) / orbit.mu);
                if (span >= period)
                {
                    sweep = 2.0 * Math.PI;
                }
                else
                {
                    double nuTo = GetAnomalyesAtTime(orbit, fromEpoch + span - orbit.startEpoch).nu;
                    sweep = Wrap(nuTo - nuFrom);
                }
            }
            else
            {
                double nuTo = GetAnomalyesAtTime(orbit, toEpoch - orbit.startEpoch).nu;
                sweep = Signed(nuTo) - Signed(nuFrom);
            }
            SampleConic(orbit, nuFrom, nuFrom + sweep, maxPoints, into);
        }

        /// <summary>
        /// Точки дуги коники в системе отсчёта центрального тела, от nuFrom до nuTo по истинной
        /// аномалии. Число точек выбирается так, чтобы соседние были видны из фокуса под углом
        /// не больше SampleAngle, но не больше maxPoints.
        /// </summary>
        public static void SampleConic(OrbitElements orbit, double nuFrom, double nuTo, int maxPoints, List<Vector3d> into)
        {
            into.Clear();
            if (Math.Abs(orbit.eccentricity - 1.0) < ParabolicTolerance) return;
            if (nuTo <= nuFrom) return;

            if (orbit.eccentricity < 1.0) SampleEllipse(orbit, nuFrom, nuTo - nuFrom, maxPoints, into);
            else SampleHyperbola(orbit, nuFrom, nuTo - nuFrom, maxPoints, into);
        }

        static void SampleEllipse(OrbitElements orbit, double nuFrom, double nuSpan, int maxPoints, List<Vector3d> into)
        {
            double e = orbit.eccentricity;
            double a = orbit.semiMajorAxis;
            double b = a * Math.Sqrt(1.0 - e * e);

            double from = EccentricFromTrue(e, nuFrom);
            double span = nuSpan >= 2.0 * Math.PI ? 2.0 * Math.PI : Wrap(EccentricFromTrue(e, nuFrom + nuSpan) - from);

            // Шаг равномерен по эксцентрической аномалии: так отрезки почти равны по длине дуги
            // при любом эксцентриситете. Угол же, видимый из фокуса, у перицентра растёт по
            // сравнению с шагом по E в sqrt((1 + e) / (1 - e)) раз — по нему и считаются точки.
            double crowding = Math.Sqrt((1.0 + e) / (1.0 - e));
            int count = PointCount(span * crowding, maxPoints);
            for (int i = 0; i < count; i++)
            {
                double anomaly = from + span * i / (count - 1.0);
                Vector3d perifocal = new(a * (Math.Cos(anomaly) - e), b * Math.Sin(anomaly), 0.0);
                into.Add(RotatePerifocalToInertial(perifocal, orbit));
            }
        }

        static void SampleHyperbola(OrbitElements orbit, double nuFrom, double nuSpan, int maxPoints, List<Vector3d> into)
        {
            double e = orbit.eccentricity;
            double limit = Math.Acos(-1.0 / e) - AsymptoteMargin;
            double from = Mathd.Clamp(Signed(nuFrom), -limit, limit);
            double to = Mathd.Clamp(Signed(nuFrom) + nuSpan, -limit, limit);
            if (to <= from) return;

            double p = Math.Abs(orbit.semiMajorAxis) * (e * e - 1.0);
            // По истинной аномалии: у гиперболы это и есть угол, видимый из фокуса.
            int count = PointCount(to - from, maxPoints);
            for (int i = 0; i < count; i++)
            {
                double nu = from + (to - from) * i / (count - 1.0);
                double r = p / (1.0 + e * Math.Cos(nu));
                into.Add(RotatePerifocalToInertial(new Vector3d(r * Math.Cos(nu), r * Math.Sin(nu), 0.0), orbit));
            }
        }

        /// <summary>
        /// Истинная аномалия, на которой коника пересекает заданный радиус: дуга внутри — это
        /// [-nu, +nu]. Возвращает pi, если коника целиком внутри радиуса, и 0, если целиком
        /// снаружи. Нужна отрисовке: за сферой влияния коника перестаёт быть траекторией.
        /// </summary>
        public static double TrueAnomalyAtRadius(OrbitElements orbit, double radius)
        {
            if (double.IsPositiveInfinity(radius)) return Math.PI;

            double e = orbit.eccentricity;
            // Круговая орбита либо целиком внутри, либо целиком снаружи: делить не на что.
            if (e < 1e-12) return orbit.semiMajorAxis <= radius ? Math.PI : 0.0;

            double p = orbit.semiMajorAxis * (1.0 - e * e);
            double cosine = (p / radius - 1.0) / e;
            if (cosine <= -1.0) return Math.PI;
            if (cosine >= 1.0) return 0.0;
            return Math.Acos(cosine);
        }

        static int PointCount(double angleSpan, int maxPoints) =>
            Math.Min(Math.Max((int)Math.Ceiling(angleSpan / SampleAngle) + 1, 2), Math.Max(maxPoints, 2));

        static double EccentricFromTrue(double e, double nu) =>
            2.0 * Math.Atan2(Math.Sqrt(1.0 - e) * Math.Sin(nu / 2.0), Math.Sqrt(1.0 + e) * Math.Cos(nu / 2.0));

        /// <summary>Угол в [0, 2pi).</summary>
        static double Wrap(double angle)
        {
            double wrapped = angle % (2.0 * Math.PI);
            return wrapped < 0.0 ? wrapped + 2.0 * Math.PI : wrapped;
        }

        /// <summary>
        /// Угол в [-pi, pi): у гиперболы истинная аномалия знаковая. Ровно -pi должен остаться
        /// отрицательным, иначе «вся дуга от асимптоты до асимптоты» вырождается в точку.
        /// </summary>
        static double Signed(double angle)
        {
            double wrapped = Wrap(angle);
            return wrapped >= Math.PI ? wrapped - 2.0 * Math.PI : wrapped;
        }

        /// <summary>Период обращения по замкнутой орбите.</summary>
        public static double Period(OrbitElements orbit) =>
            2.0 * Math.PI * Math.Sqrt(Math.Pow(orbit.semiMajorAxis, 3) / orbit.mu);

        public static (double periapsis, double apoapsis) GetPeriapsisAndApoapsis(OrbitElements elements)
        {
            return (elements.semiMajorAxis * (1 - elements.eccentricity), elements.semiMajorAxis * (1 + elements.eccentricity));
        }
    }
}
