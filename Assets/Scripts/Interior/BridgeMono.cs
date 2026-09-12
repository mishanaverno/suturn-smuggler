using OuterSpace.Sim;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Кокпит-заглушка: кубы вместо модели, собранные по схеме компоновки.
    ///
    /// Оси корабля: +Z — нос, +X — правый борт, +Y — подволок. Кресло стоит в центре,
    /// навигационный компьютер по правому борту впереди, органы управления ДУ и РСУ перед
    /// креслом, телеметрия и СОЖ по правую руку ближе к корме, выход — позади кресла.
    ///
    /// Геометрия собирается строителем, но живёт в сцене: строитель вызывается по команде из
    /// контекстного меню компонента, а не при каждом запуске. Собранное один раз становится
    /// обычными объектами, и дальше их двигают мышью. Числа в коде — стартовая раскладка
    /// и объяснение: габариты кабины, высота глаз, вынос панелей на длину руки.
    ///
    /// Кокпит стоит в начале координат и оттуда не уезжает — это и есть floating origin:
    /// движется мир, а не корабль. Пространство симуляции живёт там же и разведено с ним не
    /// расстоянием, а отсутствием общих поверхностей: его объекты лежат на слое Simulation,
    /// которого камера глаза не видит, и не несут коллайдеров вовсе.
    ///
    /// Что игра ищет в кокпите — четыре точки: место пилота, выход с него, плоскость экрана
    /// и место появления тела. Ссылки на них хранят ConsoleStation и PilotMono, поэтому
    /// объекты можно переименовывать и перевешивать, не трогая код.
    /// </summary>
    public class BridgeMono : MonoBehaviour
    {
        public static readonly Vector3 Origin = Vector3.zero;

        /// <summary>Ширина экрана навигационного компьютера, м.</summary>
        public float screenWidth = 0.42f;
        /// <summary>
        /// Ставить ли переборки и подволок. Пока кокпит разглядывают снаружи, в редакторе,
        /// они только мешают; закрытым он понадобится, когда дойдёт до света и до того,
        /// что отсек — замкнутый объём.
        /// </summary>
        public bool enclosed = false;
        /// <summary>
        /// Начинать игру на месте пилота, а не телом в невесомости. Полёт по кораблю никуда
        /// не делся — он за люком, — но игра начинается там, где происходит дело.
        /// </summary>
        public bool startSeated = true;

        public ConsoleStation station;
        public Transform spawn;
        /// <summary>Положение и разворот глаз пилота на его месте.</summary>
        public Transform pilotPoint;
        /// <summary>Куда встаёт тело, уходя с места пилота.</summary>
        public Transform exit;

        void Start()
        {
            if (station == null) station = GetComponentInChildren<ConsoleStation>();
            if (station == null)
            {
                Debug.LogWarning("Кокпит пуст. Собрать: контекстное меню компонента BridgeMono → «Собрать кокпит заново».");
                return;
            }
            EnsureActors();
            if (NavDisplayMono.instance != null && station.screen != null)
            {
                Debug.Log("yes");
                NavDisplayMono.instance.MountOn(station.screen, screenWidth);
            }
        }

        /// <summary>
        /// Тело и пилот создаются при запуске, а не хранятся в сцене: настраивать в них мышью
        /// нечего, кроме точек, а точки лежат в сцене отдельно.
        /// </summary>
        void EnsureActors()
        {
            if (PlayerMono.instance == null)
            {
                GameObject body = new("Player");
                body.transform.SetParent(transform, false);
                if (spawn != null) body.transform.SetPositionAndRotation(spawn.position, spawn.rotation);

                CharacterController controller = body.AddComponent<CharacterController>();
                controller.height = 1.7f;
                controller.radius = 0.28f;
                controller.center = new Vector3(0f, 0.85f, 0f);
                // Уступов в невесомости не бывает: подниматься по ним нечем и незачем.
                controller.slopeLimit = 90f;
                controller.stepOffset = 0f;

                body.AddComponent<PlayerMono>();
            }

            if (PilotMono.instance == null && pilotPoint != null)
            {
                GameObject pilotObject = new("Pilot");
                pilotObject.transform.SetParent(transform, false);
                pilotObject.transform.SetPositionAndRotation(pilotPoint.position, pilotPoint.rotation);
                PilotMono pilot = pilotObject.AddComponent<PilotMono>();
                pilot.exit = exit;
            }

            PilotSeat seat = GetComponentInChildren<PilotSeat>();
            if (seat != null) seat.pilot = PilotMono.instance;

            if (GetComponent<CockpitControls>() == null) gameObject.AddComponent<CockpitControls>();

            if (startSeated && PilotMono.instance != null)
            {
                // TakeOver сам погасит тело и переключит карту ввода на кокпит.
                PilotMono.instance.TakeOver();
            }
            else if (PlayerMono.instance != null)
            {
                PlayerMono.instance.SetActive(true);
            }
        }
    }
}
