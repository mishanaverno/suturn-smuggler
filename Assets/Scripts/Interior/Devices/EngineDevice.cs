using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Двигатель: включить тягу и сказать, идёт ли она.
    ///
    /// Тяга разрешена только при запланированном манёвре — это не свойство двигателя,
    /// а правило игры: прожиг исполняет план, а не заменяет его. Условие живёт здесь,
    /// потому что отказывает в нажатии именно двигатель.
    /// </summary>
    public class EngineDevice : ShipDevice
    {
        protected override void Wire()
        {
            // Переключатель, а не удержание: прожиг длится минутами, держать кнопку всё это
            // время нечем — рука нужна на других органах.
            Bind(CommandId.EngineToggle, () => Ship?.SetThrust(!Ship.Thrusting));

            // «Нажали кнопку» и «тяга идёт» — разные вещи: они расходятся при автоотсечке,
            // когда характеристическая скорость выбрана, а кнопка осталась нажатой.
            Bind(SignalId.EngineRunning, () => Ship != null && Ship.Thrusting);

            Bind(ReadingId.Acceleration, () => Ship == null ? double.NaN : Ship.Acceleration);
            Bind(ReadingId.RemainingBurn, () =>
                Ship == null || Ship.GetManeuver() == null ? double.NaN : Ship.RemainingBurnDuration);
        }

        /// <summary>
        /// Положение рычага отдаёт сам рычаг: его разъём подключён к органу, а не сюда.
        /// Двигатель это положение только исполняет — переносит в модель долю полной тяги.
        /// Нет рычага — тяга полная: корабль без органа управления не должен стоять на нуле.
        /// </summary>
        void Update()
        {
            if (Ship == null) return;
            Ship.throttle = ControlBus.TryRead(ReadingId.Throttle, out double value) ? value : 1.0;
        }

        static bool HasPlan() => Ship != null && Ship.GetManeuver() != null;
    }
}
