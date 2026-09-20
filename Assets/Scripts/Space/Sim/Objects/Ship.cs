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
        public const int MaxManeuvers = 3;
        Maneuver maneuver;
        // Направление, которое требует режим ориентации. Куда корабль смотрит на самом деле,
        // знает attitude: разворот занимает время, и на коротком прожиге тяга уходит не туда,
        // куда её планировали, пока корабль не довернулся.
        Vector3d dir = Vector3d.right;
        double attitudeEpoch;
        bool thrusting;
        // Отсечка взводится при включении двигателя и снимается на нуле остатка: иначе
        // дожечь сверх плана было бы нечем, а решение «продолжать ли» остаётся за игроком.
        bool cutoffArmed;
        double burnEpoch;
        public override bool TracksSOITransitions => true;
        public double mass;
        /// <summary>
        /// Расчётное ускорение при полной тяге, м/с². Пять g — много для человека, но пока
        /// нет ни топлива, ни перегрузок, это просто цена минуты: при 50 м/с² прожиг на
        /// 7 км/с длится две с половиной минуты, а при одном — два часа, и проверять на таком
        /// игру невозможно. Число временное и уедет, как только появится формула Циолковского.
        /// </summary>
        public const double DesignAcceleration = 50.0;
        /// <summary>Тяга двигателя, Н. Считается от массы: двигатель подбирают под корабль.</summary>
        public double thrust;
        public readonly TrajectoryCache trajectory = new();
        static readonly int DefaultMaxPatches = new PredictSettings().maxPatches;
        static readonly double DefaultHorizonPeriods = new PredictSettings().horizonPeriods;
        // Прогноз пересчитывается не каждый кадр: его вход меняется от прожига и смены
        // центрального тела, а не от хода времени.
        const int RecalculateEveryFrames = 10;
        // На прожиге орбита меняется каждый тик, и полная цепочка патч-коник пересчитывалась
        // бы шесть раз в секунду — это и были лаги. Но дело не только в цене: прогноз на
        // недели вперёд во время прожига и не нужен, он всё равно неверен через тик. Поэтому
        // на время работы двигателя прибор показывает одну дугу на виток вперёд, а полную
        // цепочку строит заново на отсечке.
        const int BurnRecalculateEveryFrames = 25;
        const double BurnHorizonPeriods = 1.0;

        public ShipOrientation orientation = ShipOrientation.Free;
        public readonly Attitude attitude = new();
        /// <summary>Команда ручного вращения по связанным осям: крен, тангаж, рыскание, каждая в [-1, 1].</summary>
        public Vector3d rotationCommand;
        /// <summary>
        /// Множитель шага настройки, выставляемый пультом: грубо, точно или как есть. Модель
        /// его не выводит из клавиш — она вообще не знает, что клавиши существуют.
        /// </summary>
        public double stepScale = 1.0;
        /// <summary>
        /// Доля полной тяги, 0…1: положение рычага на панели. Расчётное ускорение
        /// (Acceleration) остаётся паспортным — по нему считаются длительность прожига и
        /// момент его начала, то есть план. Дросселирование меняет исполнение плана, а не
        /// сам план: прожиг на половине тяги уйдёт за расчётное окно, и это видно по остатку.
        /// </summary>
        public double throttle = 1.0;

        /// <summary>Направление тяги в инерциальной системе центрального тела.</summary>
        public Vector3d Direction => attitude.Forward;
        /// <summary>Направление, которого требует режим ориентации.</summary>
        public Vector3d CommandedDirection => dir;
        public bool Thrusting => thrusting;
        public double Acceleration => thrust / mass;
        /// <summary>Сожжено с начала прожига по текущему манёвру, м/с.</summary>
        public double BurnedDeltaV { get; private set; }
        public double RemainingDeltaV => GetNextManeuver() is Maneuver next
            ? next.PlannedMagnitude - BurnedDeltaV
            : 0.0;
        /// <summary>Сколько осталось жечь при нынешней тяге, с.</summary>
        public double RemainingBurnDuration => Mathd.Max(RemainingDeltaV, 0.0) / Acceleration;
        /// <summary>
        /// Прожиг центрируется на узле: начатый в момент узла, он весь пришёлся бы на время
        /// после него, и чем длиннее — тем сильнее результат разошёлся бы с планом.
        /// </summary>
        public double BurnStartEpoch =>
            GetNextManeuver() is Maneuver next
                ? next.startEpoch - 0.5 * next.PlannedMagnitude / Acceleration
                : 0.0;
        public double CurrentTimeStep => TimeStep(ManeuverTimeSpan(), stepScale);
        public double CurrentSpeedStep => SpeedStep(SpeedAtNode(), stepScale);

        public Ship(double mass, GameObject prefab) : base(Vector3d.zero, Vector3d.zero, 0.0, prefab, new() { SpaceObjectParts.TRAJECTORY })
        {
            this.mass = mass;
            thrust = mass * DesignAcceleration;
        }
        public Maneuver GetManeuver()
        {
            return maneuver;
        }

        /// <summary>
        /// Ближайший узел для автоматики и исполнения. GetManeuver оставлен последним
        /// созданным узлом: именно его редактируют ручки навигационного компьютера.
        /// </summary>
        public Maneuver GetNextManeuver()
        {
            Maneuver next = maneuver;
            while (next?.Previous != null) next = next.Previous;
            return next;
        }

        public int ManeuverCount
        {
            get
            {
                int count = 0;
                for (Maneuver current = maneuver; current != null; current = current.Previous) count++;
                return count;
            }
        }

        public bool CanCreateManeuver => ManeuverCount < MaxManeuvers;

        /// <summary>
        /// Цель получает только последняя плановая траектория. source == null означает
        /// фактическую траекторию корабля, которая используется лишь когда плана нет.
        /// </summary>
        public SpaceObject TargetForTrajectory(Maneuver source) =>
            source == null ? (maneuver == null ? SimMono.target : null) : (maneuver == source ? SimMono.target : null);

        public void CreateManeuver(double afterEpoch)
        {
            if (!CanCreateManeuver) return;
            bool hadPlan = maneuver != null;
            double epoch = GameMono.instance.Epoch + afterEpoch;
            if (maneuver != null) epoch = Mathd.Max(epoch, maneuver.startEpoch);
            maneuver = new(this, epoch, maneuver);
            if (!hadPlan) BurnedDeltaV = 0.0;
        }
        public override void OnCentralBodyChanged(SpaceObject previous)
        {
            trajectory.Invalidate();
            if (maneuver == null) return;
            // С кораблём напрямую связан только первый узел. Остальные после его перевода
            // в новую систему отсчёта сами перестроятся по цепочке плановых траекторий.
            Maneuver first = maneuver;
            while (first.Previous != null) first = first.Previous;
            first.Reframe(previous);
        }
        public void DeleteManeuver()
        {
            if (maneuver == null) return;
            Maneuver deleted = maneuver;
            bool deletedNext = deleted.Previous == null;
            maneuver = deleted.Previous;
            if (maneuver != null) deleted.Detach();
            UnityEngine.GameObject.Destroy(deleted.GameObject);
            if (deletedNext) BurnedDeltaV = 0.0;
        }

        /// <summary>
        /// Удаляет ближайший к исполнению узел. Следующий узел становится началом плана и
        /// пересчитывает оставшуюся цепочку от текущей фактической орбиты корабля.
        /// </summary>
        public void DeleteNextManeuver()
        {
            Maneuver deleted = GetNextManeuver();
            if (deleted == null) return;
            Maneuver following = deleted.DetachNextAsFirst();
            if (maneuver == deleted) maneuver = following;
            UnityEngine.GameObject.Destroy(deleted.GameObject);
            BurnedDeltaV = 0.0;
        }
        public override void Update()
        {
            int every = thrusting ? BurnRecalculateEveryFrames : RecalculateEveryFrames;
            if (Time.frameCount % every == 0)
            {
                trajectory.Update(orbitParams, centralBody, GameMono.instance.Epoch, TargetForTrajectory(null));
            }
        }

        public void SetThrust(bool on)
        {
            if (thrusting == on) return;
            thrusting = on;
            trajectory.settings.maxPatches = on ? 1 : DefaultMaxPatches;
            trajectory.settings.horizonPeriods = on ? BurnHorizonPeriods : DefaultHorizonPeriods;
            trajectory.Invalidate();
            if (on)
            {
                burnEpoch = GameMono.instance.Epoch;
                cutoffArmed = RemainingDeltaV > 0.0;
            }
            if (GameMono.instance.TimeToggler != null) GameMono.instance.TimeToggler.SetLocked(on);
        }

        /// <summary>
        /// Приращение характеристической скорости в LVLH манёвра. Величину шага считает
        /// модель (CurrentSpeedStep), направление выбирает пульт.
        /// </summary>
        public void AddDeltaV(Vector3d delta)
        {
            maneuver.deltaLVLHVelocity += delta;
            maneuver.CalcAndDraw();
        }

        /// <summary>
        /// Ориентация пересчитывается каждый тик: режимы привязаны к движению. Исключение —
        /// Maneuver, он берёт вектор, снятый при планировании.
        /// </summary>
        public void UpdateDirection()
        {
            Vector3d direction = DirectionOf(orientation);
            if (direction.sqrMagnitude > 0.0) dir = direction;
        }

        /// <summary>
        /// Куда смотрел бы корабль в заданном режиме. Нужно не только тому режиму, который
        /// включён: шар-указатель показывает все направления сразу, и считаться они обязаны
        /// из одного места, иначе метка на приборе и разворот по той же клавише разойдутся.
        /// </summary>
        public Vector3d DirectionOf(ShipOrientation mode)
        {
            (Vector3d r, Vector3d v) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(orbitParams, GameMono.instance.Epoch);
            Vector3d closing = SimMono.target == null
                ? Vector3d.zero
                : simTransform.GLOBAL_V - SimMono.target.simTransform.GLOBAL_V;
            Maneuver next = GetNextManeuver();
            Vector3d planned = next == null ? Vector3d.zero : next.PlannedDeltaV;
            return Orientation.Direction(mode, r, v, closing, planned);
        }

        /// <summary>Мгновенно совместить тягу с направлением режима, без разворота.</summary>
        public void AlignInstantly() => attitude.Snap(dir);

        /// <summary>
        /// Разворот идёт по симуляционному времени. В режиме Free корабль слушает ручку, в
        /// остальных автопилот сам ведёт продольную ось в направление режима: это и есть
        /// разница между «держать направление» и «лететь как летится».
        /// </summary>
        void UpdateAttitude()
        {
            double epoch = GameMono.instance.Epoch;
            double dt = epoch - attitudeEpoch;
            attitudeEpoch = epoch;
            if (dt <= 0.0) return;

            if (orientation == ShipOrientation.Free) attitude.Rotate(rotationCommand, dt);
            else attitude.AlignTo(dir, dt);
        }

        public static double StepScale(bool coarse, bool fine) => coarse ? CoarseFactor : fine ? FineFactor : 1.0;
        public static double TimeStep(double span, double scale) => TimeStepFraction * span * scale;
        public static double SpeedStep(double orbitalSpeed, double scale) => SpeedStepFraction * orbitalSpeed * scale;

        /// <summary>
        /// Величина, от доли которой меряется сдвиг манёвра по времени: период орбиты, а на
        /// незамкнутой — время до выхода из сферы влияния, потому что периода там нет.
        /// </summary>
        double ManeuverTimeSpan()
        {
            // Последующие узлы живут уже не на фактической орбите корабля, а на одной из
            // дуг плановой траектории предыдущего манёвра. Шаг должен масштабироваться ею.
            if (maneuver?.Previous != null) return maneuver.SourceTimeSpan;
            if (orbitParams.eccentricity < 1.0) return AstroDynamic.Period(orbitParams);
            IReadOnlyList<TrajectoryPatch> patches = trajectory.patches;
            if (patches == null || patches.Count == 0) return trajectory.settings.openOrbitHorizon;
            return patches[0].EndEpoch - GameMono.instance.Epoch;
        }

        double SpeedAtNode() => maneuver == null ? velocity.magnitude : maneuver.SpeedAtNode;

        /// <summary>
        /// Сдвиг узла по времени. Раньше текущего момента манёвра не бывает: точка задана на
        /// будущей орбите.
        /// </summary>
        public void ShiftManeuverTime(double seconds)
        {
            if (maneuver == null || seconds == 0.0) return;
            maneuver.SetStartEpoch(Mathd.Max(maneuver.startEpoch + seconds, GameMono.instance.Epoch));
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
                if (BurnStartEpoch > epoch) nearest = Nearer(nearest, BurnStartEpoch, "BURN START");
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
            UpdateDirection();
            UpdateAttitude();
            if (thrusting) ApplyThrust();
            base.FixedUpdate();
        }

        /// <summary>
        /// Прирост скорости вдоль фиксированного инерциального направления суммируется точно:
        /// сумма импульсов равна интегралу ускорения. Приближением остаётся положение — между
        /// шагами корабль идёт по кеплеровой дуге, а не по траектории с работающим двигателем.
        /// Погрешность первого порядка по шагу, и гасится она коррекцией, как в реальном
        /// полёте. Расхождение плановой и фактической траектории здесь — не баг.
        ///
        /// Время берётся симуляционное: реальное расходилось бы с эпохой на всякой перемотке,
        /// кроме единичной.
        /// </summary>
        void ApplyThrust()
        {
            double epoch = GameMono.instance.Epoch;
            double dt = epoch - burnEpoch;
            burnEpoch = epoch;
            if (dt <= 0.0) return;

            double dv = Acceleration * Mathd.Clamp01(throttle) * dt;
            BurnedDeltaV += dv;
            (Vector3d r, Vector3d v) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(orbitParams, epoch);
            orbitParams = AstroDynamic.CalculateOrbitElements(r, v + attitude.Forward * dv, centralBody.MU, epoch);
            trajectory.Invalidate();

            if (cutoffArmed && RemainingDeltaV <= 0.0) SetThrust(false);
        }
    }
}
