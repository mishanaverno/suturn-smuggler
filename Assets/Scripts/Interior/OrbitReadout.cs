using Game;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Контроль орбиты: где корабль сейчас и куда его приведёт запланированный манёвр.
    /// Рядом, в одних единицах — чтобы разницу читать глазами, а не считать в уме.
    /// </summary>
    [RequireComponent(typeof(ReadoutPanel))]
    public class OrbitReadout : MonoBehaviour
    {
        ReadoutPanel panel;

        void Awake() => panel = GetComponent<ReadoutPanel>();

        void Update()
        {
            if (!panel.DueThisFrame) return;
            if (SimMono.playerShip is not Ship ship)
            {
                panel.Text = "NO SHIP";
                return;
            }

            string central = ship.centralBody == null ? "—" : ship.centralBody.GameObject.name;
            (double periapsis, double apoapsis) = AstroDynamic.GetPeriapsisAndApoapsis(ship.orbitParams);
            string current =
                $"ORBIT  {central.ToUpperInvariant()}\n" +
                $"PE {Km(periapsis)}\n" +
                $"AP {Km(apoapsis)}\n" +
                $"E  {ship.orbitParams.eccentricity:F4}\n" +
                $"I  {ship.orbitParams.inclination:F2}°";

            Maneuver maneuver = ship.GetManeuver();
            if (maneuver == null)
            {
                panel.Text = current + "\n\nNO MANEUVER";
                return;
            }

            (double plannedPe, double plannedAp) = AstroDynamic.GetPeriapsisAndApoapsis(maneuver.newOrbitParams);
            panel.Text = current +
                "\n\nPLANNED\n" +
                $"PE {Km(plannedPe)}\n" +
                $"AP {Km(plannedAp)}\n" +
                $"DV {ship.RemainingDeltaV:F1} / {maneuver.PlannedMagnitude:F1}\n" +
                $"T- {TrajectoryRenderer.Clock(ship.BurnStartEpoch - GameMono.instance.Epoch)}";
        }

        static string Km(double meters) => $"{meters / 1000.0:N0} km";
    }
}
