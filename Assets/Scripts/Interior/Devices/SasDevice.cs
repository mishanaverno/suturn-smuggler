using OuterSpace.Sim;

namespace Interior
{
    /// <summary>
    /// Система ориентации: куда автопилот держит нос корабля и держит ли вообще.
    ///
    /// Режим набирается двумя галетами по три положения — строка и столбец. Девять режимов
    /// ложатся на девять клеток без единой пустой; схема «ось и знак» оставляла бы дырку,
    /// потому что у манёвра обратной стороны нет.
    ///
    /// Галеты подписаны буквами, а не делом: клетка — это пересечение, и назвать её на самой
    /// ручке нечем. Что включает каждая пара, читают с таблички на панели. Пульт приходится
    /// один раз изучить — зато на нём нет ни одного положения, которое ничего не делает.
    ///
    /// Выбор и включение разделены: галеты только выбирают, за выбранное берётся отдельная
    /// кнопка. Иначе нельзя выставить режим заранее, не дёрнув корабль. Уже ведущий автопилот
    /// перенаводится сразу — щёлкнуть галетой при включённом автомате и значит «теперь туда».
    ///
    /// Своего «ведёт или нет» устройство не держит, а смотрит в корабль: режим снимает не
    /// только эта панель — взятая ручка пилота бросает автопилот, и вторая копия состояния
    /// разошлась бы с кораблём в тот же миг.
    /// </summary>
    public class SasDevice : ShipDevice
    {
        /// <summary>
        /// Раскладка галет: первый индекс — строка X, Y, Z, второй — столбец A, B, C.
        ///
        /// Правило покрывает две трети таблицы: строка — ось орбитальной системы (скорость,
        /// нормаль, радиус), столбец A — по оси, B — против. Столбец C выпадает из него:
        /// там опоры, которых может не быть вовсе, — цель, антицель и манёвр.
        ///
        /// Табличка на панели набрана руками и повторяет эту раскладку. Она не собирается
        /// отсюда, поэтому поправивший таблицу обязан поправить и её — копия лежит ниже
        /// ровно в том виде, в каком стоит на панели:
        ///
        /// <code>
        /// ┌───┬─────┬─────┬─────┐
        /// │   │  A  │  B  │  C  │
        /// ├───┼─────┼─────┼─────┤
        /// │ X │ PRO │ RET │ TGT │
        /// │ Y │ NML │ ANM │ ATG │
        /// │ Z │ RDO │ RDI │ MNV │
        /// └───┴─────┴─────┴─────┘
        /// </code>
        /// </summary>
        static readonly ShipOrientation[,] Modes =
        {
            { ShipOrientation.Prograde, ShipOrientation.Retrograde, ShipOrientation.Target },
            { ShipOrientation.Normal, ShipOrientation.Antinormal, ShipOrientation.AntiTarget },
            { ShipOrientation.RadialOut, ShipOrientation.RadialIn, ShipOrientation.Maneuver },
        };

        int row;
        int column;

        protected override void Wire()
        {
            Bind(CommandId.SasRowX, () => SetRow(0), () => CanRow(0));
            Bind(CommandId.SasRowY, () => SetRow(1), () => CanRow(1));
            Bind(CommandId.SasRowZ, () => SetRow(2), () => CanRow(2));

            Bind(CommandId.SasColumnA, () => SetColumn(0), () => CanColumn(0));
            Bind(CommandId.SasColumnB, () => SetColumn(1), () => CanColumn(1));
            Bind(CommandId.SasColumnC, () => SetColumn(2), () => CanColumn(2));

            // Выключить ведущий автопилот можно всегда: его опору — манёвр или цель — могли
            // убрать уже после включения, и кнопка, погасшая вместе с ней, заперла бы режим.
            Bind(CommandId.SasToggle, Toggle, () => Engaged() || Reachable(Aim));
            Bind(CommandId.SasHold, Hold, HasShip);

            // Связанные оси корабля: X — вперёд, Y — влево, Z — вверх. Стик от себя — нос
            // вниз, вправо — нос вправо, поворот рукояти вправо — крен вправо.
            Bind(SettingId.Pitch, value => Steer(ref Ship.rotationCommand.y, value), HasShip);
            Bind(SettingId.Yaw, value => Steer(ref Ship.rotationCommand.z, -value), HasShip);
            Bind(SettingId.Roll, value => Steer(ref Ship.rotationCommand.x, value), HasShip);

            Bind(SignalId.SasEngaged, Engaged);
            Bind(SignalId.SasHolding, Holding);
        }

        /// <summary>Выбранное галетами. Не обязательно то, чем корабль занят сейчас.</summary>
        ShipOrientation Aim => Modes[row, column];

        static bool HasShip() => Ship != null;

        static bool HasTarget() => Ship != null && SimMono.target != null;

        static bool HasPlan() => Ship != null && Ship.GetNextManeuver() != null;

        /// <summary>
        /// Есть ли куда наводиться. Клетки столбца C держатся на цели и плане: убрали цель —
        /// галета туда не доворачивается, вернули — оживает сама.
        /// </summary>
        static bool Reachable(ShipOrientation mode) => mode switch
        {
            ShipOrientation.Target or ShipOrientation.AntiTarget => HasTarget(),
            ShipOrientation.Maneuver => HasPlan(),
            _ => HasShip(),
        };

        /// <summary>Автопилот ведёт. Стабилизация сюда не входит: она никуда не наводит.</summary>
        static bool Engaged() => Ship != null
            && Ship.orientation != ShipOrientation.Free
            && Ship.orientation != ShipOrientation.Hold;

        static bool Holding() => Ship != null && Ship.orientation == ShipOrientation.Hold;

        bool CanRow(int index) => Reachable(Modes[index, column]);

        bool CanColumn(int index) => Reachable(Modes[row, index]);

        void SetRow(int index)
        {
            row = index;
            Follow();
        }

        void SetColumn(int index)
        {
            column = index;
            Follow();
        }

        void Follow()
        {
            if (Engaged()) Ship.orientation = Aim;
        }

        void Toggle() => Ship.orientation = Engaged() ? ShipOrientation.Free : Aim;

        /// <summary>
        /// Стик задаёт вращение, а не положение: отклонённый — раскручивает, отпущенный —
        /// оставляет как есть. Отклонённый стик снимает режим: иначе пилот и автопилот тянули
        /// бы корабль в разные стороны, и на органах это выглядело бы как заедание, а не как спор.
        /// </summary>
        static void Steer(ref double axis, double value)
        {
            axis = value;
            if (value != 0.0) Ship.orientation = ShipOrientation.Free;
        }

        /// <summary>
        /// Стабилизация и наведение — одна рука: включённая, она бросает автопилот, потому
        /// что гасить вращение и одновременно разворачиваться нельзя.
        /// </summary>
        static void Hold() => Ship.orientation = Holding() ? ShipOrientation.Free : ShipOrientation.Hold;
    }
}
