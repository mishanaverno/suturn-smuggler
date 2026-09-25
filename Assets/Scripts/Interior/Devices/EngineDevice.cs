using OuterSpace.Sim;

namespace Interior
{
    /// <summary>
    /// Двигательная установка: галета режима, двойной РУД, зажигание, реактор и баки.
    /// </summary>
    public class EngineDevice : ShipDevice
    {
        protected override void Wire()
        {
            BindMode(CommandId.EngineModeIdle, SignalId.EngineModeIdle, EngineMode.Idle);
            BindMode(CommandId.EngineModeCruise, SignalId.EngineModeCruise, EngineMode.Cruise);
            BindMode(CommandId.EngineModeProx, SignalId.EngineModeProx, EngineMode.Prox);
            Bind(ReadingId.ReactorPower, () => Ship == null ? double.NaN : Ship.engine.reactorPower);
            Bind(ReadingId.ReactorTemperature, () => Ship == null ? double.NaN : Ship.engine.ReactorTemperature);
            Bind(ReadingId.Methane, () => Ship == null ? double.NaN : Ship.tanks.methane);
            Bind(ReadingId.Lox, () => Ship == null ? double.NaN : Ship.tanks.lox);
            Bind(ReadingId.Thrust, () => Ship == null ? double.NaN : Ship.Thrusting ? Ship.engine.Thrust : 0.0);
            // Импульс виден и до зажигания: по нему видно, догнал ли реактор заказ.
            Bind(ReadingId.SpecificImpulse, () => Ship == null ? double.NaN : Ship.engine.SpecificImpulse);
            Bind(ReadingId.AvailableDeltaV, () => Ship == null ? double.NaN : Ship.engine.AvailableDeltaV(Ship.Mass));

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
            // тоже он: спрашивать «сколько тяги» надо у того, кто ею распоряжается. Рукоятки
            // толкают друг друга, и по этим же показаниям вторая встаёт на место.
            Bind(SettingId.Throttle, value => Ship?.engine.SetMainThrottle(value), HasShip);
            Bind(SettingId.LoxThrottle, value => Ship?.engine.SetLoxThrottle(value), HasShip);
            Bind(ReadingId.Throttle, () => Ship == null ? double.NaN : Ship.engine.MainThrottle);
            Bind(ReadingId.LoxThrottle, () => Ship == null ? double.NaN : Ship.engine.LoxThrottle);
        }

        void BindMode(CommandId select, SignalId active, EngineMode mode)
        {
            Bind(select, () => Ship?.SetEngineMode(mode));
            Bind(active, () => Ship != null && Ship.engine.Mode == mode);
        }

        static bool HasShip() => Ship != null;
    }
}
