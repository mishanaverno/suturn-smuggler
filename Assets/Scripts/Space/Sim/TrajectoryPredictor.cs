using System;
using System.Collections.Generic;
using DoublePrecision;

namespace OuterSpace.Sim
{
    /// <summary>
    /// Прогноз траектории по патчед-коникам: последовательность дуг, разделённых пересечениями
    /// границ сфер влияния, и точки наибольшего сближения с выбранной целью.
    ///
    /// Ни времени, ни состояния мира здесь нет: всё приходит параметрами. Прогноз считает
    /// положения на сотни моментов вперёд, и через текущую эпоху из синглтона он работать
    /// не может в принципе.
    /// </summary>
    public static class TrajectoryPredictor
    {
        public static List<TrajectoryPatch> Predict(
            OrbitElements startOrbit,
            SpaceObject startCentral,
            double startEpoch,
            IReadOnlyList<SpaceObject> bodies,
            PredictSettings settings)
        {
            List<TrajectoryPatch> patches = new();
            double endEpoch = startEpoch + settings.horizon;
            OrbitElements orbit = startOrbit;
            SpaceObject central = startCentral;
            double epoch = startEpoch;

            for (int i = 0; i < settings.maxPatches; i++)
            {
                TrajectoryPatch patch = new()
                {
                    Central = central,
                    Orbit = orbit,
                    StartEpoch = epoch,
                    EndEpoch = endEpoch,
                    EndReason = PatchEndReason.Horizon,
                };
                FindPatchEnd(patch, bodies, endEpoch, settings);
                patches.Add(patch);

                if (patch.NextCentral == null) break;

                orbit = ElementsAfterPatch(patch);
                central = patch.NextCentral;
                epoch = patch.EndEpoch;
            }
            return patches;
        }

        /// <summary>Элементы орбиты вокруг нового центрального тела на момент конца дуги.</summary>
        static OrbitElements ElementsAfterPatch(TrajectoryPatch patch)
        {
            double epoch = patch.EndEpoch;
            (Vector3d relR, Vector3d relV) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(patch.Orbit, epoch);
            (Vector3d centralR, Vector3d centralV) = BodyStateAt(patch.Central, epoch);
            (Vector3d nextR, Vector3d nextV) = BodyStateAt(patch.NextCentral, epoch);
            return SOITransition.ElementsForCentral(
                centralR + relR, centralV + relV, nextR, nextV, patch.NextCentral.MU, epoch);
        }

        static void FindPatchEnd(TrajectoryPatch patch, IReadOnlyList<SpaceObject> bodies, double endEpoch, PredictSettings settings)
        {
            SpaceObject central = patch.Central;
            OrbitElements orbit = patch.Orbit;
            double shipSpeed = MaxSpeed(orbit);

            // Выход считается по тому же порогу, что и в рантайме: иначе предсказанный момент
            // ухода из сферы влияния разошёлся бы с фактическим на всю ширину гистерезиса.
            double escapeRadius = central.IsRoot
                ? double.PositiveInfinity
                : central.SOI * (1.0 + SOITransition.Hysteresis);
            bool escapePossible = !central.IsRoot && Apoapsis(orbit) >= escapeRadius;
            bool impactPossible = Periapsis(orbit) <= central.radius;

            List<SpaceObject> targets = new();
            List<double> approachSpeeds = new();
            Vector3d startPosition = PositionAt(orbit, patch.StartEpoch);
            foreach (SpaceObject body in bodies)
            {
                if (body.centralBody != central) continue;
                if (settings.prefilter && !Reachable(orbit, body)) continue;
                // Тело, внутри сферы которого дуга начинается: из неё корабль только что вышел,
                // и вход в неё — не событие, а нулевой длины дуга и бесконечный цикл.
                if ((startPosition - PositionAt(body.orbitParams, patch.StartEpoch)).magnitude <= body.SOI) continue;
                targets.Add(body);
                approachSpeeds.Add(shipSpeed + MaxSpeed(body.orbitParams));
            }

            if (!escapePossible && !impactPossible && targets.Count == 0) return;

            // История трёх последних отсчётов: по двум ищется смена знака, по трём — разворот
            // расстояния, за которым может прятаться касательное сближение внутри шага.
            double[] gaps = new double[targets.Count];
            double[] previousGaps = new double[targets.Count];
            double[] olderGaps = new double[targets.Count];
            double olderTime = double.NegativeInfinity;

            double time = patch.StartEpoch;
            double radius = startPosition.magnitude;
            Gaps(orbit, targets, time, gaps);

            while (time < endEpoch)
            {
                double step = settings.maxStep;
                if (escapePossible) step = Math.Min(step, (escapeRadius - radius) / shipSpeed);
                if (impactPossible) step = Math.Min(step, (radius - central.radius) / shipSpeed);
                for (int i = 0; i < gaps.Length; i++) step = Math.Min(step, gaps[i] / approachSpeeds[i]);
                step = Math.Min(Math.Max(step, settings.minStep), settings.maxStep);

                double next = Math.Min(time + step, endEpoch);
                double nextRadius = PositionAt(orbit, next).magnitude;
                double[] rotated = olderGaps;
                olderGaps = previousGaps;
                previousGaps = gaps;
                gaps = rotated;
                Gaps(orbit, targets, next, gaps);

                double eventEpoch = double.PositiveInfinity;
                PatchEndReason reason = PatchEndReason.Horizon;
                SpaceObject nextCentral = null;

                if (impactPossible && nextRadius <= central.radius)
                {
                    eventEpoch = Bisect(t => PositionAt(orbit, t).magnitude - central.radius, time, next, settings.rootTolerance);
                    reason = PatchEndReason.Impact;
                }
                if (escapePossible && radius <= escapeRadius && nextRadius > escapeRadius)
                {
                    double escape = Bisect(t => escapeRadius - PositionAt(orbit, t).magnitude, time, next, settings.rootTolerance);
                    if (escape < eventEpoch)
                    {
                        eventEpoch = escape;
                        reason = PatchEndReason.EscapedSOI;
                        nextCentral = central.centralBody;
                    }
                }
                for (int i = 0; i < gaps.Length; i++)
                {
                    SpaceObject body = targets[i];
                    double from = time;
                    double to = next;
                    if (gaps[i] > 0.0)
                    {
                        bool turned = olderTime > double.NegativeInfinity
                            && previousGaps[i] < olderGaps[i] && gaps[i] > previousGaps[i];
                        if (!turned) continue;
                        double minimum = Minimize(t => Gap(orbit, body, t), olderTime, next, settings.rootTolerance);
                        if (Gap(orbit, body, minimum) > 0.0) continue;
                        from = olderTime;
                        to = minimum;
                    }
                    double entry = Bisect(t => Gap(orbit, body, t), from, to, settings.rootTolerance);
                    if (entry >= eventEpoch) continue;
                    eventEpoch = entry;
                    reason = PatchEndReason.EnteredSOI;
                    nextCentral = body;
                }

                if (!double.IsPositiveInfinity(eventEpoch))
                {
                    patch.EndEpoch = eventEpoch;
                    patch.EndReason = reason;
                    patch.NextCentral = nextCentral;
                    return;
                }

                olderTime = time;
                time = next;
                radius = nextRadius;
            }
        }

        static void Gaps(OrbitElements orbit, List<SpaceObject> targets, double epoch, double[] result)
        {
            if (targets.Count == 0) return;
            Vector3d ship = PositionAt(orbit, epoch);
            for (int i = 0; i < targets.Count; i++)
            {
                result[i] = (ship - PositionAt(targets[i].orbitParams, epoch)).magnitude - targets[i].SOI;
            }
        }

        static double Gap(OrbitElements orbit, SpaceObject body, double epoch) =>
            (PositionAt(orbit, epoch) - PositionAt(body.orbitParams, epoch)).magnitude - body.SOI;

        /// <summary>Коника не достаёт до кольца, в котором ходит тело, — встречи быть не может.</summary>
        static bool Reachable(OrbitElements orbit, SpaceObject body)
        {
            double bodyPeriapsis = Periapsis(body.orbitParams);
            double bodyApoapsis = Apoapsis(body.orbitParams);
            return Periapsis(orbit) <= bodyApoapsis + body.SOI && Apoapsis(orbit) >= bodyPeriapsis - body.SOI;
        }

        static Vector3d PositionAt(OrbitElements orbit, double epoch) =>
            AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(orbit, epoch).r;

        static double Periapsis(OrbitElements orbit) => orbit.semiMajorAxis * (1.0 - orbit.eccentricity);

        static double Apoapsis(OrbitElements orbit) =>
            orbit.eccentricity < 1.0 ? orbit.semiMajorAxis * (1.0 + orbit.eccentricity) : double.PositiveInfinity;

        /// <summary>Скорость в перицентре: гарантированная верхняя оценка скорости на конике.</summary>
        static double MaxSpeed(OrbitElements orbit) =>
            Math.Sqrt(orbit.mu * (2.0 / Periapsis(orbit) - 1.0 / orbit.semiMajorAxis));

        /// <summary>Верхняя оценка абсолютной скорости тела: своя коника плюс движение родителей.</summary>
        static double SpeedBound(SpaceObject body) =>
            body.IsRoot ? body.simTransform.GLOBAL_V.magnitude : MaxSpeed(body.orbitParams) + SpeedBound(body.centralBody);

        /// <summary>Абсолютное состояние тела: сложение по цепочке родителей.</summary>
        public static (Vector3d r, Vector3d v) BodyStateAt(SpaceObject body, double epoch)
        {
            // Корень системы неподвижен, его состояние от эпохи не зависит.
            if (body.IsRoot) return (body.simTransform.GLOBAL_R, body.simTransform.GLOBAL_V);
            (Vector3d relR, Vector3d relV) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(body.orbitParams, epoch);
            (Vector3d parentR, Vector3d parentV) = BodyStateAt(body.centralBody, epoch);
            return (parentR + relR, parentV + relV);
        }

        /// <summary>Абсолютное состояние корабля на той дуге прогноза, которая покрывает момент.</summary>
        public static (Vector3d r, Vector3d v) ShipStateAt(IReadOnlyList<TrajectoryPatch> patches, double epoch)
        {
            TrajectoryPatch patch = patches[patches.Count - 1];
            for (int i = 0; i < patches.Count; i++)
            {
                if (epoch > patches[i].EndEpoch) continue;
                patch = patches[i];
                break;
            }
            (Vector3d relR, Vector3d relV) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(patch.Orbit, epoch);
            (Vector3d centralR, Vector3d centralV) = BodyStateAt(patch.Central, epoch);
            return (centralR + relR, centralV + relV);
        }

        /// <summary>
        /// Точки наибольшего сближения с целью на всём протяжении прогноза. У цели может не быть
        /// ни массы, ни сферы влияния, поэтому ищется не корень расстояния, а его минимум.
        /// </summary>
        public static List<CloseApproach> FindCloseApproaches(
            IReadOnlyList<TrajectoryPatch> patches,
            SpaceObject target,
            PredictSettings settings)
        {
            List<CloseApproach> approaches = new();
            // Минимум расстояния до собственного центрального тела — это перицентр, и целиться
            // в него отдельным механизмом незачем.
            foreach (TrajectoryPatch patch in patches)
            {
                if (patch.Central == target) return approaches;
            }

            double speedBound = 0.0;
            foreach (TrajectoryPatch patch in patches)
            {
                speedBound = Math.Max(speedBound, MaxSpeed(patch.Orbit) + SpeedBound(patch.Central) + SpeedBound(target));
            }

            // Прогноз может оборваться раньше горизонта — столкновением или лимитом дуг,
            // и искать сближение за пределом предсказанного нечем.
            double endEpoch = patches[patches.Count - 1].EndEpoch;
            double time = patches[0].StartEpoch;
            double distance = Separation(patches, target, time);
            double previousTime = time;
            double previousDistance = distance;
            double olderTime = double.NegativeInfinity;
            double closest = distance;
            double farthest = distance;

            while (time < endEpoch)
            {
                double step = Math.Min(Math.Max(distance / speedBound, settings.minStep), settings.maxStep);
                double next = Math.Min(time + step, endEpoch);
                double nextDistance = Separation(patches, target, next);

                if (olderTime > double.NegativeInfinity && distance < previousDistance && distance <= nextDistance)
                {
                    double epoch = Minimize(t => Separation(patches, target, t), previousTime, next, settings.rootTolerance);
                    approaches.Add(Describe(patches, target, epoch));
                }

                olderTime = previousTime;
                previousTime = time;
                previousDistance = distance;
                time = next;
                distance = nextDistance;
                closest = Math.Min(closest, distance);
                farthest = Math.Max(farthest, distance);
            }

            // Соорбитальная цель: расстояние почти постоянно, любой найденный минимум — шум.
            if (farthest > 0.0 && (farthest - closest) / farthest < settings.coorbitalVariation) approaches.Clear();
            if (approaches.Count > settings.maxApproaches)
                approaches.RemoveRange(settings.maxApproaches, approaches.Count - settings.maxApproaches);
            return approaches;
        }

        static CloseApproach Describe(IReadOnlyList<TrajectoryPatch> patches, SpaceObject target, double epoch)
        {
            (Vector3d shipR, Vector3d shipV) = ShipStateAt(patches, epoch);
            (Vector3d targetR, Vector3d targetV) = BodyStateAt(target, epoch);
            return new CloseApproach
            {
                Epoch = epoch,
                Distance = (shipR - targetR).magnitude,
                RelativeSpeed = (shipV - targetV).magnitude,
                ShipPosition = shipR,
                TargetPosition = targetR,
            };
        }

        static double Separation(IReadOnlyList<TrajectoryPatch> patches, SpaceObject target, double epoch) =>
            (ShipStateAt(patches, epoch).r - BodyStateAt(target, epoch).r).magnitude;

        /// <summary>Корень f на интервале, где f меняет знак с плюса на минус.</summary>
        static double Bisect(Func<double, double> f, double from, double to, double tolerance)
        {
            while (to - from > tolerance)
            {
                double middle = 0.5 * (from + to);
                if (f(middle) > 0.0) from = middle; else to = middle;
            }
            return 0.5 * (from + to);
        }

        /// <summary>Минимум унимодальной на интервале функции золотым сечением.</summary>
        static double Minimize(Func<double, double> f, double from, double to, double tolerance)
        {
            const double Ratio = 0.6180339887498949;
            double left = to - Ratio * (to - from);
            double right = from + Ratio * (to - from);
            double leftValue = f(left);
            double rightValue = f(right);
            while (to - from > tolerance)
            {
                if (leftValue < rightValue)
                {
                    to = right;
                    right = left;
                    rightValue = leftValue;
                    left = to - Ratio * (to - from);
                    leftValue = f(left);
                }
                else
                {
                    from = left;
                    left = right;
                    leftValue = rightValue;
                    right = from + Ratio * (to - from);
                    rightValue = f(right);
                }
            }
            return 0.5 * (from + to);
        }
    }
}
