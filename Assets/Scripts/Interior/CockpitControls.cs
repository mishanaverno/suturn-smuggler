using DoublePrecision;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Проводка кокпита: что делает каждый орган управления. Панель — это железо, смысл
    /// железа лежит здесь, одним перечнем. Так кубик на панели можно двигать и
    /// переименовывать в редакторе, не трогая поведение, а весь список того, что умеет
    /// кокпит, читается на одном экране.
    ///
    /// Органы находятся по имени объекта: имя задаёт строитель, и оно же служит адресом.
    /// Если органа в сцене нет, проводка молчит — недостающая кнопка не должна ронять игру.
    /// </summary>
    public class CockpitControls : MonoBehaviour
    {
        /// <summary>Шаг разворота вида по нажатию крестовины, градусов.</summary>
        public float viewStep = 15f;

        static Ship Ship => SimMono.playerShip as Ship;
        static NavDisplayMono Nav => NavDisplayMono.instance;

        void Start()
        {
            Knob("LeverNodeTime", "node time",
                direction => Ship?.ShiftManeuverTime(direction * Ship.CurrentTimeStep), HasManeuver);
            Knob("LeverRange", "display range",
                direction => Nav?.ShiftRange(direction));
            Knob("LeverDeltaVX", "delta-v X",
                direction => AddDeltaV(new Vector3d(direction, 0, 0)), HasManeuver);
            Knob("LeverDeltaVY", "delta-v Y",
                direction => AddDeltaV(new Vector3d(0, direction, 0)), HasManeuver);
            Knob("LeverDeltaVZ", "delta-v Z",
                direction => AddDeltaV(new Vector3d(0, 0, direction)), HasManeuver);

            Button("ButtonNew", "new maneuver", () => Ship?.CreateManeuver(0));
            // Кнопка-переключатель, а не удержание: прожиг длится минутами, держать кнопку
            // всё это время нечем — рука нужна на других органах.
            Button("ButtonBurn", "engine", () => Ship?.SetThrust(!Ship.Thrusting), HasManeuver);

            Button("ViewYawPlus", "view right", () => Nav?.Rotate(new Vector2(viewStep, 0f)));
            Button("ViewYawMinus", "view left", () => Nav?.Rotate(new Vector2(-viewStep, 0f)));
            Button("ViewPitchPlus", "view up", () => Nav?.Rotate(new Vector2(0f, viewStep)));
            Button("ViewPitchMinus", "view down", () => Nav?.Rotate(new Vector2(0f, -viewStep)));

            Lamp("LampDeltaV", () => Ship != null && Ship.RemainingDeltaV > 0.0);
            Lamp("LampPlan", HasManeuver);
        }

        static bool HasManeuver() => Ship != null && Ship.GetManeuver() != null;

        static void AddDeltaV(Vector3d direction)
        {
            Ship ship = Ship;
            if (ship == null || ship.GetManeuver() == null) return;
            ship.AddDeltaV(direction * ship.CurrentSpeedStep);
        }

        void Knob(string name, string prompt, System.Action<int> onStep, System.Func<bool> available = null)
        {
            PanelKnob knob = Find<PanelKnob>(name);
            if (knob == null) return;
            knob.label = prompt;
            knob.onStep = onStep;
            knob.available = available;
        }

        void Button(string name, string prompt, System.Action onPress, System.Func<bool> available = null)
        {
            PanelButton button = Find<PanelButton>(name);
            if (button == null) return;
            button.label = prompt;
            button.onPress = onPress;
            button.available = available;
        }

        void Lamp(string name, System.Func<bool> lit)
        {
            PanelLamp lamp = Find<PanelLamp>(name);
            if (lamp != null) lamp.lit = lit;
        }

        T Find<T>(string name) where T : Component
        {
            foreach (T candidate in GetComponentsInChildren<T>(true))
            {
                if (candidate.name == name) return candidate;
            }
            Debug.LogWarning($"В кокпите нет органа «{name}» — проводка для него пропущена.");
            return null;
        }
    }
}
