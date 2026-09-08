using System.Collections.Generic;
using DoublePrecision;
using Game;
using UnityEngine;

namespace OuterSpace.Sim.Objects
{
    public class Ship : SpaceObject
    {
        /// <summary>Ближайшее событие, перед которым перемотку надо притормозить.</summary>
        public sealed class WarpEvent
        {
            public double Epoch;
            public string Reason;
        }

        // Шаг считается не абсолютной константой, а долей той величины, к которой применяется:
        // процент периода той орбиты, на которой стоит манёвр, и процент орбитальной скорости
        // в его точке. Иначе одно и то же нажатие на парковочной орбите и на перелётной
        // означает несопоставимое.
        public const double TimeStepFraction = 0.01;
        public const double SpeedStepFraction = 0.01;
        public const double CoarseFactor = 10.0;
        public const double FineFactor = 0.1;
        // Ускорение от тяги, м/с². Конечная тяга через массу — предмет отдельной задачи.
        const double ThrustAcceleration = 100.0;
        // Нажатие двигает манёвр на шаг, удержание — на десять шагов в секунду. Без автоповтора
        // до нужного момента пришлось бы дощёлкивать сотнями нажатий.
        const float RepeatDelay = 0.4f;
        const float RepeatRate = 10f;
        float holdTime;
        private double dv = 0;
        Maneuver maneuver;
        double F = 0;
        Vector3d dir = Vector3d.right;
        public override bool TracksSOITransitions => true;
        public double mass;
        public readonly TrajectoryCache trajectory = new();
        // Прогноз пересчитывается не каждый кадр: его вход меняется от прожига и смены
        // центрального тела, а не от хода времени.
        const int RecalculateEveryFrames = 10;

        public ShipOrientation orientation = ShipOrientation.Free;
        /// <summary>Направление тяги в инерциальной системе центрального тела.</summary>
        public Vector3d Direction => dir;
        public double CurrentTimeStep => TimeStep(ManeuverTimeSpan(), StepScale());
        public double CurrentSpeedStep => SpeedStep(SpeedAtNode(), StepScale());

        public Ship(double mass, GameObject prefab) : base(Vector3d.zero, Vector3d.zero, 0.0, prefab, new() { SpaceObjectParts.TRAJECTORY })
        {
            this.mass = mass;
        }
        public Maneuver GetManeuver()
        {
            return maneuver;
        }
        public void CreateManeuver(double afterEpoch)
        {
            maneuver = new(this, GameMono.instance.Epoch + afterEpoch);
        }
        public override void OnCentralBodyChanged(SpaceObject previous)
        {
            trajectory.Invalidate();
            if (maneuver != null) maneuver.Reframe(previous);
        }
        public void DeleteManeuver()
        {
            UnityEngine.GameObject.Destroy(maneuver.GameObject);
            maneuver = null;
        }
        public override void Update()
        {
            // Цель — состояние корабля, а не манёвра: манёвр после исполнения удаляется, а
            // сближения нужнее всего как раз на финальных коррекциях, когда его уже нет.
            if (Time.frameCount % RecalculateEveryFrames == 0)
            {
                trajectory.Update(orbitParams, centralBody, GameMono.instance.Epoch, SimMono.target);
            }

            ReadOrientation();
            UpdateDirection();

            if (Input.GetKeyDown(KeyCode.Space)) F = ThrustAcceleration;
            if (Input.GetKeyUp(KeyCode.Space)) F = 0;

            if (Input.GetKeyUp(KeyCode.M))
            {
                CreateManeuver(0);
            }

            if (Input.GetKeyUp(KeyCode.R) && maneuver != null)
            {
                DeleteManeuver();
            }

            if (maneuver == null) return;

            double step = CurrentSpeedStep;
            if (Input.GetKeyUp(KeyCode.W)) AddDeltaV(new Vector3d(step, 0, 0));
            if (Input.GetKeyUp(KeyCode.S)) AddDeltaV(new Vector3d(-step, 0, 0));
            if (Input.GetKeyUp(KeyCode.D)) AddDeltaV(new Vector3d(0, 0, step));
            if (Input.GetKeyUp(KeyCode.A)) AddDeltaV(new Vector3d(0, 0, -step));

            double shift = ManeuverTimeShift();
            // Раньше текущего момента манёвра не бывает: точка задана на будущей орбите.
            if (shift != 0)
            {
                maneuver.SetStartEpoch(Mathd.Max(maneuver.startEpoch + shift, GameMono.instance.Epoch));
            }
        }

        void AddDeltaV(Vector3d delta)
        {
            maneuver.deltaLVLHVelocity += delta;
            maneuver.CalcAndDraw();
        }

        // Смены режима — отдельные клавиши, а не перебор по кругу: перебор из десяти режимов
        // хуже, чем десять клавиш.
        void ReadOrientation()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) orientation = ShipOrientation.Prograde;
            if (Input.GetKeyDown(KeyCode.Alpha2)) orientation = ShipOrientation.Retrograde;
            if (Input.GetKeyDown(KeyCode.Alpha3)) orientation = ShipOrientation.Normal;
            if (Input.GetKeyDown(KeyCode.Alpha4)) orientation = ShipOrientation.Antinormal;
            if (Input.GetKeyDown(KeyCode.Alpha5)) orientation = ShipOrientation.RadialOut;
            if (Input.GetKeyDown(KeyCode.Alpha6)) orientation = ShipOrientation.RadialIn;
            if (Input.GetKeyDown(KeyCode.Alpha7)) orientation = ShipOrientation.Target;
            if (Input.GetKeyDown(KeyCode.Alpha8)) orientation = ShipOrientation.AntiTarget;
            if (Input.GetKeyDown(KeyCode.Alpha9)) orientation = ShipOrientation.Maneuver;
            if (Input.GetKeyDown(KeyCode.Alpha0)) orientation = ShipOrientation.Free;
        }

        /// <summary>
        /// Ориентация пересчитывается каждый тик: режимы привязаны к движению. Исключение —
        /// Maneuver, он берёт вектор, снятый при планировании.
        /// </summary>
        public void UpdateDirection()
        {
            (Vector3d r, Vector3d v) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(orbitParams, GameMono.instance.Epoch);
            Vector3d closing = SimMono.target == null
                ? Vector3d.zero
                : simTransform.GLOBAL_V - SimMono.target.simTransform.GLOBAL_V;
            Vector3d planned = maneuver == null ? Vector3d.zero : maneuver.PlannedDeltaV;
            Vector3d direction = Orientation.Direction(orientation, r, v, closing, planned);
            if (direction.sqrMagnitude > 0.0) dir = direction;
        }

        public static double StepScale(bool coarse, bool fine) => coarse ? CoarseFactor : fine ? FineFactor : 1.0;
        public static double TimeStep(double span, double scale) => TimeStepFraction * span * scale;
        public static double SpeedStep(double orbitalSpeed, double scale) => SpeedStepFraction * orbitalSpeed * scale;

        static double StepScale() => StepScale(Input.GetKey(KeyCode.LeftShift), Input.GetKey(KeyCode.LeftControl));

        /// <summary>
        /// Величина, от доли которой меряется сдвиг манёвра по времени: период орбиты, а на
        /// незамкнутой — время до выхода из сферы влияния, потому что периода там нет.
        /// </summary>
        double ManeuverTimeSpan()
        {
            if (orbitParams.eccentricity < 1.0) return AstroDynamic.Period(orbitParams);
            IReadOnlyList<TrajectoryPatch> patches = trajectory.patches;
            if (patches == null || patches.Count == 0) return trajectory.settings.openOrbitHorizon;
            return patches[0].EndEpoch - GameMono.instance.Epoch;
        }

        double SpeedAtNode() => maneuver == null ? velocity.magnitude : maneuver.SpeedAtNode;

        double ManeuverTimeShift()
        {
            int direction = (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0);
            if (direction == 0)
            {
                holdTime = 0f;
                return 0.0;
            }
            double span = CurrentTimeStep;
            bool pressed = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Q);
            holdTime += Time.deltaTime;
            if (pressed) return direction * span;
            if (holdTime < RepeatDelay) return 0.0;
            return direction * span * RepeatRate * Time.deltaTime;
        }

        /// <summary>
        /// Ближайшее событие на собственной траектории корабля плюс начало прожига по манёвру.
        /// События на плановой траектории манёвра сюда не входят: они гипотетические, корабль
        /// на той траектории пока не находится, и тормозить перед ними нельзя.
        /// </summary>
        public WarpEvent NextEvent(double epoch)
        {
            WarpEvent nearest = null;
            IReadOnlyList<TrajectoryPatch> patches = trajectory.patches;
            if (patches != null)
            {
                foreach (TrajectoryPatch patch in patches)
                {
                    if (patch.EndReason == PatchEndReason.Horizon || patch.EndEpoch <= epoch) continue;
                    nearest = Nearer(nearest, patch.EndEpoch, PatchEventText(patch));
                    break;
                }
            }
            IReadOnlyList<CloseApproach> approaches = trajectory.approaches;
            if (approaches != null)
            {
                foreach (CloseApproach approach in approaches)
                {
                    if (approach.Epoch <= epoch) continue;
                    nearest = Nearer(nearest, approach.Epoch, "CLOSE APPROACH");
                    break;
                }
            }
            if (maneuver != null)
            {
                // Прожиг центрируется на узле, поэтому событие — его начало, а не сам узел.
                double burnStart = maneuver.startEpoch - 0.5 * maneuver.PlannedDeltaV.magnitude / ThrustAcceleration;
                if (burnStart > epoch) nearest = Nearer(nearest, burnStart, "BURN START");
            }
            return nearest;
        }

        static WarpEvent Nearer(WarpEvent current, double epoch, string reason) =>
            current != null && current.Epoch <= epoch ? current : new WarpEvent { Epoch = epoch, Reason = reason };

        static string PatchEventText(TrajectoryPatch patch)
        {
            switch (patch.EndReason)
            {
                case PatchEndReason.EnteredSOI: return $"SOI ENTRY {patch.NextCentral.GameObject.name}";
                case PatchEndReason.EscapedSOI: return $"SOI EXIT {patch.Central.GameObject.name}";
                default: return "IMPACT";
            }
        }

        /// <summary>
        /// Самое узкое окно пролёта сферы влияния при нынешней скорости корабля: сфера Мимаса
        /// проходится насквозь за 35 секунд, и на большой перемотке этого хватает, чтобы
        /// пройти её между двумя тиками.
        /// </summary>
        public double NarrowestFlybyWindow()
        {
            double speed = simTransform.GLOBAL_V.magnitude;
            double narrowest = double.PositiveInfinity;
            foreach (SpaceObject body in SimMono.bodies)
            {
                narrowest = Mathd.Min(narrowest, 2.0 * body.SOI / (speed + body.simTransform.GLOBAL_V.magnitude));
            }
            return narrowest;
        }

        public override void FixedUpdate()
        {
            if (F > 0)
            {
                dv += F * Time.deltaTime;
                if (GameMono.instance.Epoch - orbitParams.startEpoch > 1)
                {
                    // dir задан в инерциальной системе центрального тела, разворачивать его
                    // через LVLH не нужно: базис LVLH едет вместе с орбитой.
                    (Vector3d cr, Vector3d cv) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(orbitParams, GameMono.instance.Epoch);
                    orbitParams = AstroDynamic.CalculateOrbitElements(cr, cv + dir * dv, centralBody.MU, GameMono.instance.Epoch);
                    dv = 0;
                    trajectory.Invalidate();
                }
            }
            base.FixedUpdate();
        }
    }
}
