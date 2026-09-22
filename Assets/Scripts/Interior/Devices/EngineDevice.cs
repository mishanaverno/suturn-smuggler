namespace Interior
{
    /// <summary>
    /// Двигатель: включить тягу, принять её долю от рычага и сказать, идёт ли она.
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

            // Долю тяги выставляет рычаг, а хранит её двигатель. Поэтому показание отдаёт
            // тоже он: рычаг физически стоит, где стоит, но спрашивать «сколько тяги»
            // надо у того, кто ею распоряжается.
            Bind(SettingId.Throttle, SetThrottle, HasShip);
            Bind(ReadingId.Throttle, () => Ship == null ? double.NaN : Ship.throttle);
        }

        static bool HasShip() => Ship != null;

        static void SetThrottle(double value)
        {
            if (Ship != null) Ship.throttle = value;
        }
    }
}
