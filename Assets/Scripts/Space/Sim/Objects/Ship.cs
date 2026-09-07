using DoublePrecision;
using Game;
using UnityEngine;

namespace OuterSpace.Sim.Objects
{
    public class Ship : SpaceObject
    {
        private double DVSPAN = 100;
        private double dv = 0;
        Maneuver maneuver;
        double F = 0;
        Vector3d dir = Vector3d.right;
        public Ship(Vector3d position, double mass, GameObject prefab) : base(position, Vector3d.zero, mass, prefab, new() { SpaceObjectParts.ORBIT })
        {
        }
        public Maneuver GetManeuver()
        {
            return maneuver;
        }
        public void CreateManeuver(double afterEpoch)
        {
            maneuver = new(this, GameMono.instance.Epoch + afterEpoch);
        }
        public void DeleteManeuver()
        {
            UnityEngine.GameObject.Destroy(maneuver.GameObject);
            maneuver = null;
        }
        public override void Update()
        {
            if (Input.GetKeyUp(KeyCode.M))
            {
                CreateManeuver(100000);
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
                }
                
            }
            base.FixedUpdate();
        }
    }
}
