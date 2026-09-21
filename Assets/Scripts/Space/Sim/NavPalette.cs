using UnityEngine;

namespace OuterSpace.Sim
{
    /// <summary>
    /// Цвет на приборе — роль, а не принадлежность. Раньше цветов было столько же, сколько
    /// вещей: зелёные орбиты, оранжевый Сатурн, маджента корабля. Ни Сатурн, ни корабль не
    /// события, но тянули внимание сильнее всего, а различать по цвету «это Сатурн» не надо:
    /// у него подпись. Различать надо другое — что здесь моё, что выбрано и что опасно.
    ///
    /// Ролей четыре, и больше их быть не должно: пятая отнимает смысл у четырёх. Свои и чужие
    /// траектории — один тон, разной яркости: они одной природы, и различать их цветом
    /// значило бы сказать, что они разные вещи. Тревожное — красный, и только оно: красный,
    /// которым покрашено что-то ещё, перестаёт означать тревогу.
    ///
    /// Компонент, а не таблица констант: цвета подбирают глазами, на живой картинке, и
    /// перебирать их пересборкой — значит не перебрать. Лежит он на любом объекте сцены в
    /// одном экземпляре. Без него прибор работает на запасных значениях, а не чернеет:
    /// палитра, забытая в сцене, не должна выглядеть как сломанная отрисовка.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class NavPalette : MonoBehaviour
    {
        static readonly Color DefaultOwn = new(0.45f, 1f, 0.8f);
        static readonly Color DefaultOther = new(0.2f, 0.46f, 0.37f);
        static readonly Color DefaultTarget = new(1f, 0.78f, 0.3f);
        static readonly Color DefaultAlarm = new(1f, 0.32f, 0.24f);
        static readonly Color DefaultNeutral = new(0.12f, 0.17f, 0.15f);
        static readonly Color DefaultManeuver1 = new(0.75f, 0.45f, 1f);
        static readonly Color DefaultManeuver2 = new(1f, 0.55f, 0.2f);
        static readonly Color DefaultManeuver3 = new(0.25f, 1f, 0.55f);
        const float DefaultNameLevel = 0.7f;
        const float DefaultLeaderLevel = 0.4f;

        public static NavPalette instance;

        [Header("Roles")]
        [Tooltip("Своя траектория и своя метка.")]
        public Color own = DefaultOwn;
        [Tooltip("Чужие орбиты и тела — тот же тон, приглушённый.")]
        public Color other = DefaultOther;
        [Tooltip("Выбранная цель и всё, что относится к ней.")]
        public Color target = DefaultTarget;
        [Tooltip("Пересечение границы сферы влияния, столкновение. Больше ничего.")]
        public Color alarm = DefaultAlarm;
        [Tooltip("Заливка тела, на которое сейчас не смотрят.")]
        public Color neutral = DefaultNeutral;

        [Header("Maneuvers")]
        // Манёвры различаются цветом по номеру, а не по роли: номер — это адрес, по которому
        // игрок ищет узел в таблице плана и на панели, и картинка обязана отвечать тем же
        // адресом. Единственное место на приборе, где цвет означает принадлежность.
        [Tooltip("Траектория первого, ближайшего манёвра.")]
        public Color maneuver1 = DefaultManeuver1;
        [Tooltip("Траектория второго манёвра.")]
        public Color maneuver2 = DefaultManeuver2;
        [Tooltip("Траектория третьего и дальше.")]
        public Color maneuver3 = DefaultManeuver3;

        [Header("Text levels")]
        /// <summary>
        /// Ступени яркости внутри роли. Решения принимают по числам, поэтому число светится
        /// в полную силу своей роли. Имя отвечает только «что это» и стоит ступенью ниже.
        /// Выноска — ниточка от имени к объекту, и она ниже ещё на ступень: своего смысла
        /// у неё нет вовсе, а яркостью вровень с текстом она тянет взгляд на себя.
        /// </summary>
        [Tooltip("Яркость имени в долях от яркости числа.")]
        public float nameLevel = DefaultNameLevel;
        [Tooltip("Яркость выноски в долях от яркости числа.")]
        public float leaderLevel = DefaultLeaderLevel;

        void Awake() => instance = this;

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static Color Own => instance == null ? DefaultOwn : instance.own;
        public static Color Other => instance == null ? DefaultOther : instance.other;
        public static Color Target => instance == null ? DefaultTarget : instance.target;
        public static Color Alarm => instance == null ? DefaultAlarm : instance.alarm;
        public static Color Neutral => instance == null ? DefaultNeutral : instance.neutral;
        public static float NameLevel => instance == null ? DefaultNameLevel : instance.nameLevel;
        public static float LeaderLevel => instance == null ? DefaultLeaderLevel : instance.leaderLevel;

        public static Color Maneuver(int sequenceIndex)
        {
            if (instance == null)
            {
                return sequenceIndex == 0 ? DefaultManeuver1
                    : sequenceIndex == 1 ? DefaultManeuver2 : DefaultManeuver3;
            }
            return sequenceIndex == 0 ? instance.maneuver1
                : sequenceIndex == 1 ? instance.maneuver2 : instance.maneuver3;
        }

        public static Color For(SpaceObject obj)
        {
            if (obj == null) return Other;
            if (ReferenceEquals(obj, SimMono.playerShip)) return Own;
            if (ReferenceEquals(obj, SimMono.target)) return Target;
            return Other;
        }

        /// <summary>Приглушить, не трогая прозрачности: линии прибора рисуются без смешивания.</summary>
        public static Color Dim(Color color, float amount) =>
            new(color.r * amount, color.g * amount, color.b * amount, color.a);
    }
}
