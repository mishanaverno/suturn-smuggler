using DoublePrecision;
using Game;
using UnityEngine;

namespace OuterSpace.Sim.Objects
{
    public class Ship : SpaceObject
    {
        private double DVSPAN = 100;
        private double EPOCHSPAN = 600;
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
        const double DefaultHorizon = 30.0 * 86400.0;
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
            // Цель прицеливания у корабля не показывается: рандеву сводят манёвром, и две пары
            // меток сближения на экране означали бы одно и то же дважды.
            if (Time.frameCount % RecalculateEveryFrames == 0)
            {
                trajectory.settings.horizon = HorizonAhead();
                trajectory.Update(orbitParams, centralBody, GameMono.instance.Epoch, null);
            }

            if (Input.GetKeyUp(KeyCode.M))
            {
                CreateManeuver(0);
                Debug.Log(maneuver);
            }

            if (Input.GetKeyUp(KeyCode.R) && maneuver != null)
            {
                DeleteManeuver();
            }

            if (maneuver == null) return;

            if (Input.GetKeyUp(KeyCode.W))
            {
                GetManeuver().deltaLVLHVelocity.x += DVSPAN;
                Debug.Log(maneuver.deltaLVLHVelocity);
                GetManeuver().CalcAndDraw();
            }
            if (Input.GetKeyUp(KeyCode.S))
            {
                GetManeuver().deltaLVLHVelocity.x -= DVSPAN;
                Debug.Log(maneuver.deltaLVLHVelocity);
                GetManeuver().CalcAndDraw();
            }
            if (Input.GetKeyUp(KeyCode.D))
            {
                GetManeuver().deltaLVLHVelocity.z += DVSPAN;
                Debug.Log(maneuver.deltaLVLHVelocity);
                GetManeuver().CalcAndDraw();
            }
            if (Input.GetKeyUp(KeyCode.A))
            {
                GetManeuver().deltaLVLHVelocity.z -= DVSPAN;
                Debug.Log(maneuver.deltaLVLHVelocity);
                GetManeuver().CalcAndDraw();
            }
            double shift = ManeuverTimeShift();
            // Раньше текущего момента манёвра не бывает: точка задана на будущей орбите.
            if (shift != 0)
            {
                maneuver.SetStartEpoch(Mathd.Max(maneuver.startEpoch + shift, GameMono.instance.Epoch));
            }
            if (Input.GetKeyDown(KeyCode.Space))
            {
                dir = GetManeuver().deltaLVLHVelocity.normalized;
                F = 100;
                Debug.Log($"F={F}");
            }
            if (Input.GetKeyUp(KeyCode.Space))
            {
                F = 0;
                Debug.Log($"F={F}");
            }

        }
        /// <summary>
        /// Три витка вперёд: события ищутся не на абстрактные тридцать суток, а на ближайшие
        /// обороты, и окно едет вместе с кораблём. На разомкнутой траектории витка нет —
        /// там остаётся горизонт по умолчанию.
        /// </summary>
        double HorizonAhead()
        {
            if (orbitParams.eccentricity >= 1.0) return DefaultHorizon;
            double period = 2.0 * Mathd.PI * Mathd.Sqrt(Mathd.Pow(orbitParams.semiMajorAxis, 3) / orbitParams.mu);
            return 3.0 * period;
        }

        double ManeuverTimeShift()
        {
            int direction = (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0);
            if (direction == 0)
            {
                holdTime = 0f;
                return 0.0;
            }
            bool pressed = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Q);
            holdTime += Time.deltaTime;
            if (pressed) return direction * EPOCHSPAN;
            if (holdTime < RepeatDelay) return 0.0;
            return direction * EPOCHSPAN * RepeatRate * Time.deltaTime;
        }

        public override void FixedUpdate()
        {
            if (F > 0)
            {
                dv += F * Time.deltaTime;
                if (GameMono.instance.Epoch - orbitParams.startEpoch > 1)
                {
                    Vector3d deltaV = dir * dv;
                    dv = 0;
                    (Vector3d cr, Vector3d cv) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(orbitParams, GameMono.instance.Epoch);
                    Vector3d nv = cv + CoordinateConverter.LocalDeltaVtoRelative(deltaV, cr, cv);

                    //AstroDynamic.CalculateAnomaliesAtEpoch(orbitParams, centralBody.MU, GameMono.instance.Epoch - orbitParams.startEpoch, out _, out _, out double nu);
                    Debug.Log($"NU = {Mathd.Rad2Deg},\n" +
                        $"Epoch = {GameMono.instance.Epoch},\n" +
                        $"Orbit Start Epoch = {orbitParams.startEpoch},\n" +
                        //$"Orbit Start NU = {orbitParams.trueAnomaly},\n" +
                        $"R = {cr} = {CoordinateConverter.RelativeToLocal(cr, cr, cv)}\n," +
                        $"V = {cv} + {deltaV} = {nv}\n");
                    orbitParams = AstroDynamic.CalculateOrbitElements(cr, nv, centralBody.MU, GameMono.instance.Epoch);
                    trajectory.Invalidate();
                }
                
            }
            base.FixedUpdate();
        }
    }
}
