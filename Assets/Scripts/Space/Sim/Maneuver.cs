using System.Collections.Generic;
using DoublePrecision;
using Game;
using OuterSpace.Sim.Objects;
using UnityEngine;
using Utilities;

namespace OuterSpace.Sim
{
    public class Maneuver : ObjectWithMono<ManeuverMono, Maneuver>
    {
        public SimTransform simTransform;
        public OrbitElements newOrbitParams;
        public Vector3d deltaLVLHVelocity = Vector3d.zero;
        public SpaceObject spaceObject;
        public double startEpoch;
        public readonly TrajectoryCache trajectory = new();

        /// <summary>Предыдущий узел плана; null у первого манёвра корабля.</summary>
        public Maneuver Previous { get; private set; }
        /// <summary>Центральное тело той дуги плановой траектории, на которой стоит узел.</summary>
        public SpaceObject CentralBody { get; private set; }
        /// <summary>Орбита до импульса: по ней расположен и перемещается этот узел.</summary>
        public OrbitElements SourceOrbit { get; private set; }

        double sourceArcStartEpoch;
        double sourceArcEndEpoch;

        Maneuver next;

        // Точка манёвра относительно центрального тела, снятая при планировании. Манёвр — цель,
        // к которой игрок ведёт корабль: пока идёт прожиг, орбита корабля меняется, а точка стоит.
        // В simTransform её не сложить: там хранятся глобальные координаты, а центральное тело движется.
        Vector3d relativePosition;
        Vector3d relativeVelocity;

        /// <summary>Плановый импульс в инерциальной системе центрального тела, снятый при планировании.</summary>
        public Vector3d PlannedDeltaV { get; private set; }

        /// <summary>Полная характеристическая скорость манёвра, м/с.</summary>
        public double PlannedMagnitude => PlannedDeltaV.magnitude;

        /// <summary>Позиция в плане: 0 — ближайший, 1 — следующий, 2 — последний.</summary>
        public int SequenceIndex
        {
            get
            {
                int index = 0;
                for (Maneuver current = Previous; current != null; current = current.Previous) index++;
                return index;
            }
        }

        /// <summary>Орбитальная скорость в точке манёвра: от неё меряется шаг настройки Δv.</summary>
        public double SpeedAtNode => relativeVelocity.magnitude;

        public Maneuver(SpaceObject spaceObject, double startEpoch, Maneuver previous = null)
        {
            this.spaceObject = spaceObject;
            Previous = previous;
            if (previous != null) previous.next = this;
            InstatiateGameObject(ResourcesLoader.LoadPrefab($"Sim/Maneuver"));
            GameObject.transform.parent = SimMono.instance.transform;
            simTransform = new(Vector3d.zero, Vector3d.zero, GameObject.transform);
            SetStartEpoch(startEpoch);
        }

        /// <summary>
        /// Перенос манёвра по времени. Первый узел едет по орбите корабля, каждый следующий —
        /// по результату предыдущего манёвра, в том числе через границы сфер влияния.
        /// </summary>
        public void SetStartEpoch(double epoch)
        {
            startEpoch = Previous == null ? epoch : Mathd.Max(epoch, Previous.startEpoch);
            OrbitElements orbit;
            if (Previous == null)
            {
                if (spaceObject is Ship ship)
                {
                    // Первый узел тоже может находиться не на нынешней конике корабля, а на
                    // следующем patch-conic участке после выхода/входа в SOI.
                    ship.trajectory.Update(ship.orbitParams, ship.centralBody,
                        GameMono.instance.Epoch, ship.TargetForTrajectory(null));
                    TrajectoryPatch patch = PatchAt(ship.trajectory.patches, startEpoch);
                    CentralBody = patch.Central;
                    orbit = patch.Orbit;
                    sourceArcStartEpoch = patch.StartEpoch;
                    sourceArcEndEpoch = patch.EndEpoch;
                }
                else
                {
                    CentralBody = spaceObject.centralBody;
                    orbit = spaceObject.orbitParams;
                    sourceArcStartEpoch = double.NaN;
                    sourceArcEndEpoch = double.NaN;
                }
            }
            else
            {
                Previous.UpdateTrajectory(null);
                TrajectoryPatch patch = PatchAt(Previous.trajectory.patches, startEpoch);
                CentralBody = patch.Central;
                orbit = patch.Orbit;
                sourceArcStartEpoch = patch.StartEpoch;
                sourceArcEndEpoch = patch.EndEpoch;
            }
            SourceOrbit = orbit;
            simTransform.RelativeTo = CentralBody.simTransform;
            (relativePosition, relativeVelocity) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(orbit, startEpoch);
            CalcAndDraw();
            // Не ждать LateUpdate: после переподчинения цепочки старая глобальная позиция
            // относится к прежней орбите и визуально оставляет узел висеть в пространстве.
            FollowCentralBody();
        }

        static TrajectoryPatch PatchAt(IReadOnlyList<TrajectoryPatch> patches, double epoch)
        {
            TrajectoryPatch patch = patches[patches.Count - 1];
            for (int i = 0; i < patches.Count; i++)
            {
                if (epoch > patches[i].EndEpoch) continue;
                patch = patches[i];
                break;
            }
            return patch;
        }

        /// <summary>
        /// Масштаб шага перемещения узла: период его входной орбиты, а для открытой — полная
        /// длительность дуги. Если брать остаток до её конца, шаг будет каждый раз уменьшаться
        /// и узел станет асимптотически приближаться к границе, никогда её не пересекая.
        /// </summary>
        public double SourceTimeSpan
        {
            get
            {
                if (SourceOrbit.eccentricity < 1.0) return AstroDynamic.Period(SourceOrbit);
                double duration = sourceArcEndEpoch - sourceArcStartEpoch;
                return duration > 0.0 ? duration : trajectory.settings.openOrbitHorizon;
            }
        }

        /// <summary>Переносит точку манёвра в систему отсчёта нового центрального тела корабля.</summary>
        public void Reframe(SpaceObject previousCentral)
        {
            // Узел мог заранее оказаться на патче за этим переходом — SetStartEpoch подбирает
            // его по прогнозу корабля ещё до фактического пересечения SOI. Тогда узел уже не
            // на previousCentral, и сдвиг координат на разницу тел только испортил бы позицию.
            if (CentralBody != previousCentral) return;

            SimTransform current = spaceObject.centralBody.simTransform;
            relativePosition += previousCentral.simTransform.GLOBAL_R - current.GLOBAL_R;
            relativeVelocity += previousCentral.simTransform.GLOBAL_V - current.GLOBAL_V;
            CentralBody = spaceObject.centralBody;
            simTransform.RelativeTo = current;
            // PlannedDeltaV не пересчитывается: обе системы отсчёта инерциальны и отличаются
            // только началом координат, так что смена сферы влияния вектор тяги не трогает.
            // Пересчёт через LVLH развернул бы его: базис привязан к радиус-вектору.
            Recalculate();
            FollowCentralBody();
            next?.Rebase();
        }

        /// <summary>Отцепляет удаляемый последний узел от оставшегося плана.</summary>
        public void Detach() => Previous.next = null;

        /// <summary>
        /// Снимает первый узел и превращает следующий в корень плана. Новый первый узел
        /// перестраивается от фактической орбиты корабля, затем каскадно обновляет остальные.
        /// </summary>
        public Maneuver DetachNextAsFirst()
        {
            Maneuver following = next;
            next = null;
            if (following == null) return null;
            following.Previous = null;
            following.Rebase();
            return following;
        }

        /// <summary>
        /// Немедленно обновляет прогнозы всей пересаженной цепочки. Invalidate оставляет
        /// старые патчи доступными до очередного периодического пересчёта, поэтому одного
        /// Rebase недостаточно: линии несколько кадров рисовались в прежней системе отсчёта.
        /// </summary>
        public void RefreshTrajectoryChain(SpaceObject target)
        {
            FollowCentralBody();
            UpdateTrajectory(next == null ? target : null);
            next?.RefreshTrajectoryChain(target);
        }

        /// <summary>Точка манёвра задана относительно центрального тела, а оно движется.</summary>
        public void FollowCentralBody()
        {
            simTransform.SetRELATIVE_R(relativePosition);
            simTransform.SetRELATIVE_V(relativeVelocity);
        }

        [ContextMenu("Update velocity")]
        public void CalcAndDraw()
        {
            PlannedDeltaV = CoordinateConverter.LocalDeltaVtoRelative(deltaLVLHVelocity, relativePosition, relativeVelocity);
            Recalculate();
            next?.Rebase();
        }

        void Rebase() => SetStartEpoch(startEpoch);

        void Recalculate()
        {
            newOrbitParams = AstroDynamic.CalculateOrbitElements(relativePosition, relativeVelocity + PlannedDeltaV, CentralBody.MU, startEpoch);
            trajectory.Invalidate();
        }

        /// <summary>
        /// Прогноз считается от гипотетической орбиты манёвра, а не от текущего состояния корабля:
        /// игрок крутит deltaLVLHVelocity и должен видеть, куда приведёт получившаяся траектория.
        /// </summary>
        public void UpdateTrajectory(SpaceObject target) =>
            trajectory.Update(newOrbitParams, CentralBody, startEpoch, target);
    }
}
