using Game;

namespace Interior
{
    /// <summary>
    /// РСУ: сдвигает корабль, не поворачивая его. Три оси — вперёд-назад, вправо-влево,
    /// вверх-вниз, у каждой тяга фиксированная: отклонённая ось включает сопла, отпущенная
    /// выключает. Величину принимают от стика, со знаком: плюс — вперёд, вправо, вверх.
    /// </summary>
    public class RcsDevice : ShipDevice
    {
        protected override void Wire()
        {
            // Связанные оси корабля: X — вперёд, Y — влево, Z — вверх.
            Bind(SettingId.RcsForward, value => Fire(ref Ship.translationCommand.x, value), HasShip);
            Bind(SettingId.RcsRight, value => Fire(ref Ship.translationCommand.y, -value), HasShip);
            Bind(SettingId.RcsUp, value => Fire(ref Ship.translationCommand.z, value), HasShip);

            Bind(SettingId.RcsThrottle, value => Ship.rcsThrottle = value, HasShip);
            Bind(ReadingId.RcsThrottle, () => Ship == null ? double.NaN : Ship.rcsThrottle);
        }

        static bool HasShip() => Ship != null;

        /// <summary>
        /// Импульс считается по симуляционному времени, и на перемотке одно нажатие дало бы
        /// сотни м/с. Поэтому включение сопел сбрасывает перемотку в 1×. Не запирает, как
        /// маршевый: РСУ работает рывками, и между ними перемотка снова свободна.
        /// </summary>
        static void Fire(ref double axis, double value)
        {
            axis = value;
            if (value == 0.0 || GameMono.instance == null || GameMono.instance.TimeToggler == null) return;
            GameMono.instance.TimeToggler.RealTime();
        }
    }
}
