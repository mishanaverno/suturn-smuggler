using DoublePrecision;
using Game;
using Interior;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using UnityEngine;

namespace Controls
{
    /// <summary>
    /// Оператор за пультом: превращает намерения карты Console в вызовы модели. Раньше это
    /// делали сами Ship, NavDisplayPanel и TimeToggler, каждый по-своему; здесь всё в одном
    /// месте, и видно, что именно умеет пульт.
    ///
    /// Компонент работает всегда, но карта Console включена только пока игрок сидит в кресле,
    /// поэтому проверять контекст отдельно не нужно: выключенное действие не срабатывает.
    /// </summary>
    public class ShipConsoleMono : MonoBehaviour
    {
        // Нажатие двигает величину на шаг, удержание — на десять шагов в секунду. Без
        // автоповтора до нужного момента пришлось бы дощёлкивать сотнями нажатий.
        const float RepeatDelay = 0.4f;
        const float ManeuverRepeatRate = 10f;
        const float WarpRepeatRate = 8f;

        static readonly ShipOrientation[] Modes =
        {
            ShipOrientation.Prograde, ShipOrientation.Retrograde,
            ShipOrientation.Normal, ShipOrientation.Antinormal,
            ShipOrientation.RadialOut, ShipOrientation.RadialIn,
            ShipOrientation.Target, ShipOrientation.AntiTarget,
            ShipOrientation.Maneuver, ShipOrientation.Free,
        };

        float warpHold;
        int warpRepeats;

        void Awake() => GameInput.Initialize();

        void Update()
        {
            if (SimMono.playerShip is not Ship ship) return;

            ship.stepScale = Ship.StepScale(GameInput.Coarse.IsPressed(), GameInput.Fine.IsPressed());

            ReadNavDisplay();
            ReadWarp();
            ReadOrientation(ship);
            ReadRotation(ship);
            ReadThrust(ship);
            ReadManeuver(ship);
        }

        /// <summary>
        /// Клавиша цели двигает курсор списка, но не подтверждает выбор: активная цель
        /// назначается отдельной кнопкой на панели.
        /// </summary>
        void ReadNavDisplay()
        {
            NavDisplayPanel nav = NavDisplayPanel.instance;
            if (nav == null) return;
            if (GameInput.CycleFocus.WasPressedThisFrame()) nav.CycleFocus();
            if (GameInput.CycleTarget.WasPressedThisFrame()) TargetListPanel.instance?.MoveCursor(1);
        }

        void ReadWarp()
        {
            TimeToggler time = GameMono.instance == null ? null : GameMono.instance.TimeToggler;
            if (time == null) return;

            float axis = GameInput.TimeWarp.ReadValue<float>();
            int direction = axis > 0.5f ? 1 : axis < -0.5f ? -1 : 0;
            if (direction == 0)
            {
                warpHold = 0f;
                warpRepeats = 0;
                return;
            }
            int steps = direction * (GameInput.Decade.IsPressed() ? TimeToggler.StepsPerDecade : 1);
            if (GameInput.TimeWarp.WasPressedThisFrame())
            {
                time.Shift(steps);
                return;
            }
            warpHold += Time.deltaTime;
            if (warpHold < RepeatDelay || (warpHold - RepeatDelay) * WarpRepeatRate < warpRepeats) return;
            warpRepeats++;
            time.Shift(steps);
        }

        static void ReadOrientation(Ship ship)
        {
            for (int i = 0; i < Modes.Length; i++)
            {
                if (GameInput.Orientation[i].WasPressedThisFrame()) ship.orientation = Modes[i];
            }
        }

        /// <summary>
        /// Ручка задаёт вращение, а не положение: отклонённая — раскручивает, отпущенная —
        /// оставляет как есть. Гасят вращение тоже ею, в другую сторону, или режимом
        /// ориентации — автопилот останавливает корабль сам.
        ///
        /// Взятая ручка снимает режим: иначе пилот и автопилот тянули бы корабль в разные
        /// стороны, и на органах это выглядело бы как заедание, а не как спор.
        /// </summary>
        static void ReadRotation(Ship ship)
        {
            Vector2 stick = GameInput.Rotate.ReadValue<Vector2>();
            float roll = GameInput.Roll.ReadValue<float>();
            // Связанные оси корабля: X — вперёд, Y — влево, Z — вверх. W даёт нос вниз,
            // A — нос влево, E — крен вправо, как у ручки самолёта.
            ship.rotationCommand = new Vector3d(roll, stick.y, -stick.x);
            if (ship.rotationCommand.sqrMagnitude > 0.0) ship.orientation = ShipOrientation.Free;
        }

        static void ReadThrust(Ship ship)
        {
            if (GameInput.Thrust.WasPressedThisFrame()) ship.SetThrust(true);
            if (GameInput.Thrust.WasReleasedThisFrame()) ship.SetThrust(false);
        }

        /// <summary>
        /// Создать и удалить манёвр клавиши пока могут: кнопки на панели делают то же самое,
        /// но отладке нужен путь, не требующий сначала прицелиться курсором. Настройка Δv
        /// и узла по времени — только крутилками.
        /// </summary>
        static void ReadManeuver(Ship ship)
        {
            if (GameInput.ManeuverCreate.WasPressedThisFrame()) ship.CreateManeuver(0);
            if (GameInput.ManeuverDelete.WasPressedThisFrame() && ship.GetManeuver() != null)
            {
                ship.DeleteManeuver();
            }
        }

    }
}
