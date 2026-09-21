using UnityEngine;

namespace OuterSpace.Sim
{
    /// <summary>
    /// Цвет на приборе — роль, а не принадлежность. Раньше цветов было столько же, сколько
    /// вещей: зелёные орбиты, оранжевый Сатурн, маджента корабля. Ни Сатурн, ни корабль не
    /// события, но тянули внимание сильнее всего, а различать по цвету «это Сатурн» не надо:
    /// у него подпись. Различать надо другое — что здесь моё, что выбрано и что опасно.
    ///
    /// Поэтому ролей четыре, и больше их быть не должно: пятая отнимает смысл у четырёх.
    /// Свои и чужие траектории — один тон, разной яркости: они одной природы, и различать их
    /// цветом значило бы сказать, что они разные вещи. Тревожное — красный, и только оно:
    /// красный, которым покрашено что-то ещё, перестаёт означать тревогу.
    /// </summary>
    public static class NavPalette
    {
        /// <summary>Своя траектория и своя метка.</summary>
        public static readonly Color Own = new(0.45f, 1f, 0.8f);
        /// <summary>Чужие орбиты и тела — тот же тон, приглушённый.</summary>
        public static readonly Color Other = new(0.2f, 0.46f, 0.37f);
        /// <summary>Выбранная цель.</summary>
        public static readonly Color Target = new(1f, 0.78f, 0.3f);
        /// <summary>Пересечение границы сферы влияния, столкновение. Больше ничего.</summary>
        public static readonly Color Alarm = new(1f, 0.32f, 0.24f);
        /// <summary>Заливка тела, на которое сейчас не смотрят.</summary>
        public static readonly Color Neutral = new(0.12f, 0.17f, 0.15f);

        public static Color For(SpaceObject obj)
        {
            if (obj == null) return Other;
            if (ReferenceEquals(obj, SimMono.playerShip)) return Own;
            if (ReferenceEquals(obj, SimMono.target)) return Target;
            return Other;
        }

        /// <summary>
        /// Ступени яркости внутри роли. Решения принимают по числам, поэтому число светится
        /// в полную силу своей роли. Имя отвечает только «что это» и стоит ступенью ниже.
        /// Выноска — ниточка от имени к объекту, и она ниже ещё на ступень: своего смысла
        /// у неё нет вовсе, а яркостью вровень с текстом она тянет взгляд на себя.
        /// </summary>
        public const float NameLevel = 0.7f;
        public const float LeaderLevel = 0.4f;

        /// <summary>Приглушить, не трогая прозрачности: линии прибора рисуются без смешивания.</summary>
        public static Color Dim(Color color, float amount) =>
            new(color.r * amount, color.g * amount, color.b * amount, color.a);
    }
}
