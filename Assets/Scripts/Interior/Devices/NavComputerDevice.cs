using DoublePrecision;
using Game;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Навигационный компьютер: план полёта и то, как игрок смотрит на него на экране.
    ///
    /// Разворот вида — тоже его дело, а не отдельного «экрана»: экран показывает то, что
    /// компьютер считает, и своей воли у него нет.
    ///
    /// Шаги всех ручек считаются здесь и от текущего состояния орбиты: процент от длительности
    /// манёвра, процент от орбитальной скорости. Поэтому шаг и не может лежать в ассете —
    /// он меняется в полёте.
    /// </summary>
    public class NavComputerDevice : ShipDevice
    {
        [Tooltip("Шаг разворота вида по нажатию крестовины, градусов.")]
        public float viewStep = 15f;

        protected override void Wire()
        {
            Bind(CommandId.NewManeuver, () => Ship?.CreateManeuver(0),
                () => Ship != null && Ship.CanCreateManeuver);
            Bind(CommandId.RemoveManeuver, () => Ship?.DeleteManeuver(), HasPlan);
            Bind(CommandId.NextManeuver, () => Ship?.DeleteNextManeuver(), HasPlan);

            Bind(CommandId.ViewYawPlus, () => Nav?.Rotate(new Vector2(viewStep, 0f)));
            Bind(CommandId.ViewYawMinus, () => Nav?.Rotate(new Vector2(-viewStep, 0f)));
            Bind(CommandId.ViewPitchPlus, () => Nav?.Rotate(new Vector2(0f, viewStep)));
            Bind(CommandId.ViewPitchMinus, () => Nav?.Rotate(new Vector2(0f, -viewStep)));

            Bind(StepId.NavRange, direction => Nav?.ShiftRange(direction));
            Bind(StepId.ManeuverTime,
                direction => Ship?.ShiftManeuverTime(direction * Ship.CurrentTimeStep), HasPlan);
            Bind(StepId.DeltaVX, direction => AddDeltaV(new Vector3d(direction, 0, 0)), HasPlan);
            Bind(StepId.DeltaVY, direction => AddDeltaV(new Vector3d(0, direction, 0)), HasPlan);
            Bind(StepId.DeltaVZ, direction => AddDeltaV(new Vector3d(0, 0, direction)), HasPlan);

            Bind(SignalId.ManeuverPlanned, HasPlan);
            Bind(SignalId.DeltaVRemaining, () => Ship != null && Ship.RemainingDeltaV > 0.0);
            Bind(SignalId.Maneuver1Created, () => Ship != null && Ship.ManeuverCount >= 1);
            Bind(SignalId.Maneuver2Created, () => Ship != null && Ship.ManeuverCount >= 2);
            Bind(SignalId.Maneuver3Created, () => Ship != null && Ship.ManeuverCount >= 3);

            Bind(ReadingId.NavRange, () => Nav == null ? double.NaN : Nav.Range);
            Bind(ReadingId.TimeToNode, TimeToNode);
            Bind(ReadingId.DeltaVX, () => Plan()?.deltaLVLHVelocity.x ?? double.NaN);
            Bind(ReadingId.DeltaVY, () => Plan()?.deltaLVLHVelocity.y ?? double.NaN);
            Bind(ReadingId.DeltaVZ, () => Plan()?.deltaLVLHVelocity.z ?? double.NaN);
            Bind(ReadingId.RemainingDeltaV, () => Ship == null ? double.NaN : Ship.RemainingDeltaV);
            Bind(ReadingId.ShipOrbitEccentricity, () => Ship.orbitParams.eccentricity);
            Bind(ReadingId.ShipOrbitSemiMajorAxis, () => Ship.orbitParams.semiMajorAxis);
        }

        static Maneuver Plan() => Ship?.GetManeuver();

        static bool HasPlan() => Plan() != null;

        /// <summary>
        /// Время до узла манёвра. Без плана показания нет — NaN, и табло гаснет: пустая
        /// строка честнее нуля, который выглядит как «прямо сейчас».
        /// </summary>
        static double TimeToNode()
        {
            Maneuver plan = Plan();
            if (plan == null || GameMono.instance == null) return double.NaN;
            return plan.startEpoch - GameMono.instance.Epoch;
        }

        static void AddDeltaV(Vector3d direction)
        {
            Ship ship = Ship;
            if (ship == null || ship.GetManeuver() == null) return;
            ship.AddDeltaV(direction * ship.CurrentSpeedStep);
        }
    }
}
