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

        // Тесная кабина: всё в пределах вытянутой руки, встать в полный рост нельзя.
        const float Width = 2.2f;
        const float Length = 2.4f;
        const float Height = 1.9f;
        const float Wall = 0.1f;
        // Нос сужается: передняя переборка у́же кормовой, борта сходятся скосами.
        const float NoseWidth = 1.3f;
        const float Chamfer = 0.5f;
        const float HatchWidth = 0.8f;
        const float HatchHeight = 1.5f;
        // Глаза пилота над палубой. Сидит он пристёгнутым, поэтому это не рост, а посадка.
        const float EyeLevel = 1.15f;

        /// <summary>Ширина экрана навигационного компьютера, м.</summary>
        public float screenWidth = 0.42f;
        /// <summary>
        /// Ставить ли переборки и подволок. Пока кокпит разглядывают снаружи, в редакторе,
        /// они только мешают; закрытым он понадобится, когда дойдёт до света и до того,
        /// что отсек — замкнутый объём.
        /// </summary>
        public bool enclosed = false;

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

            if (PlayerMono.instance != null) PlayerMono.instance.SetActive(true);
        }

        [ContextMenu("Собрать кокпит заново")]
        public void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
            transform.position = Origin;
            BuildHull();
            BuildSeat();
            BuildPanels();
            BuildHatch();
            spawn = Anchor("Spawn", transform, new Vector3(0f, 0.9f, -1.9f), Quaternion.identity);
        }

        void BuildHull()
        {
            // Палуба остаётся и без переборок: в невесомости она не пол, а поверхность,
            // от которой считаются высоты кресла, панелей и глаз.
            Box("Deck", transform, new Vector3(0f, -Wall * 0.5f, 0f), new Vector3(Width, Wall, Length), 0.18f);

            GameObject lamp = new("CockpitLight");
            lamp.transform.SetParent(transform, false);
            lamp.transform.localPosition = new Vector3(0f, Height - 0.25f, 0.2f);
            Light light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            // Без переборок свет уходит в пустоту, поэтому в открытом кокпите он дальнобойнее.
            light.range = enclosed ? 6f : 20f;
            light.intensity = enclosed ? 1.6f : 2.2f;
            light.color = new Color(1f, 0.93f, 0.82f);

            if (!enclosed) return;

            float sideLength = Length - Chamfer;
            float sideCenter = -Chamfer * 0.5f;
            Box("Ceiling", transform, new Vector3(0f, Height + Wall * 0.5f, 0f), new Vector3(Width, Wall, Length), 0.10f);
            Box("HullPort", transform, new Vector3(-(Width + Wall) * 0.5f, Height * 0.5f, sideCenter),
                new Vector3(Wall, Height, sideLength), 0.30f);
            Box("HullStarboard", transform, new Vector3((Width + Wall) * 0.5f, Height * 0.5f, sideCenter),
                new Vector3(Wall, Height, sideLength), 0.30f);

            // Скосы к носу: ширина уходит с Width до NoseWidth на длине Chamfer.
            float inset = (Width - NoseWidth) * 0.5f;
            float run = Mathf.Sqrt(inset * inset + Chamfer * Chamfer);
            float yaw = Mathf.Atan2(inset, Chamfer) * Mathf.Rad2Deg;
            float chamferZ = Length * 0.5f - Chamfer * 0.5f;
            Chamfered("HullChamferStarboard", (Width - inset) * 0.5f, chamferZ, -yaw, run);
            Chamfered("HullChamferPort", -(Width - inset) * 0.5f, chamferZ, yaw, run);

            Box("BulkheadFore", transform, new Vector3(0f, Height * 0.5f, (Length + Wall) * 0.5f),
                new Vector3(NoseWidth, Height, Wall), 0.26f);

            float filler = (Width - HatchWidth) * 0.5f;
            Box("BulkheadAftPort", transform, new Vector3(-(HatchWidth + filler) * 0.5f, Height * 0.5f, -(Length + Wall) * 0.5f),
                new Vector3(filler, Height, Wall), 0.26f);
            Box("BulkheadAftStarboard", transform, new Vector3((HatchWidth + filler) * 0.5f, Height * 0.5f, -(Length + Wall) * 0.5f),
                new Vector3(filler, Height, Wall), 0.26f);
            Box("BulkheadAftTop", transform, new Vector3(0f, (Height + HatchHeight) * 0.5f, -(Length + Wall) * 0.5f),
                new Vector3(HatchWidth, Height - HatchHeight, Wall), 0.26f);
        }

        void Chamfered(string name, float x, float z, float yaw, float run)
        {
            GameObject box = Box(name, transform, new Vector3(x, Height * 0.5f, z), new Vector3(Wall, Height, run), 0.30f);
            box.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        void BuildSeat()
        {
            GameObject seat = new("Seat");
            seat.transform.SetParent(transform, false);

            Box("Pan", seat.transform, new Vector3(0f, 0.42f, 0f), new Vector3(0.55f, 0.08f, 0.5f), 0.14f);
            Box("Back", seat.transform, new Vector3(0f, 0.85f, -0.28f), new Vector3(0.55f, 0.9f, 0.08f), 0.14f);

            // Глаза пилота: посадка, а не рост. Смотрит он в нос корабля.
            pilotPoint = Anchor("PilotPoint", seat.transform, new Vector3(0f, EyeLevel, 0.02f), Quaternion.identity);
        }

        void BuildPanels()
        {
            BuildNavCluster();
            BuildFlightPanel();
            BuildSystemsPanel();
        }

        /// <summary>
        /// Блок навигационного компьютера по правому борту: большой экран в крайнем правом
        /// положении, два малых рядом с ним. Большой показывает сцену и потому требует камеры
        /// и текстуры; малые показывают строки и обходятся холстом — рисовать буквы камерой
        /// в текстуру, чтобы потом показать текстуру на плоскости, было бы лишним кругом.
        /// </summary>
        void BuildNavCluster()
        {
            GameObject console = new("NavConsole");
            console.transform.SetParent(transform, false);
            console.transform.localPosition = new Vector3(0.88f, 1.05f, 0.1f);
            // Разворот не задан числом, а посчитан: если кресло или блок подвинут, экраны
            // по-прежнему будут смотреть на пилота. Иначе они тихо начали бы смотреть мимо,
            // и это незаметно, пока не сядешь.
            Vector3 eye = pilotPoint != null ? pilotPoint.localPosition : new Vector3(0f, EyeLevel, 0f);
            // Холст интерфейса рисуется двусторонним, поэтому ось Z экрана смотрит туда же,
            // куда смотрит пилот, — от него, а не на него: развёрнутый навстречу холст виден
            // с изнанки, то есть зеркально.
            console.transform.localRotation = Quaternion.LookRotation(console.transform.localPosition - eye, Vector3.up);

            float mainHeight = screenWidth * 0.75f;
            Transform screen = Anchor("Screen", console.transform, new Vector3(0.22f, 0f, 0f), Quaternion.identity);
            Bezel("BezelMain", console.transform, new Vector3(0.22f, 0f, 0.02f), screenWidth, mainHeight);

            station = console.AddComponent<ConsoleStation>();
            station.screen = screen;
            station.targets = SmallScreen("ScreenTargets", console.transform, new Vector3(-0.16f, 0.09f, 0f), 0.22f, 0.14f);
            station.orbit = SmallScreen("ScreenOrbit", console.transform, new Vector3(-0.16f, -0.09f, 0f), 0.22f, 0.2f);
            station.targets.gameObject.AddComponent<TargetListPanel>();
            station.orbit.gameObject.AddComponent<OrbitReadout>();

            BuildNavControls(console.transform);
        }

        ReadoutPanel SmallScreen(string name, Transform parent, Vector3 localPosition, float width, float height)
        {
            Bezel($"Bezel{name}", parent, localPosition + new Vector3(0f, 0f, 0.02f), width, height);
            GameObject screen = new(name);
            screen.transform.SetParent(parent, false);
            screen.transform.localPosition = localPosition;
            ReadoutPanel panel = screen.AddComponent<ReadoutPanel>();
            panel.widthMeters = width;
            panel.heightMeters = height;
            return panel;
        }

        void Bezel(string name, Transform parent, Vector3 localPosition, float width, float height)
            => Box(name, parent, localPosition, new Vector3(width + 0.03f, height + 0.03f, 0.02f), 0.06f);

        /// <summary>
        /// Органы навигационного компьютера — под его экранами, а не на центральном пульте:
        /// рука и глаз работают вместе, и рычаг дальности должен лежать под той картинкой,
        /// которую он меняет. Полка наклонена к пилоту: на неё смотрят и по ней же ведут
        /// курсором.
        ///
        /// Органы — отдельные предметы, а не картинка: рычаги тянут за верх или за низ,
        /// кнопки жмут, лампы отвечают. Что именно делает каждый, знает не панель,
        /// а CockpitControls.
        /// </summary>
        void BuildNavControls(Transform console)
        {
            GameObject panel = new("NavControls");
            panel.transform.SetParent(console, false);
            panel.transform.localPosition = new Vector3(0f, -0.3f, -0.05f);
            panel.transform.localRotation = Quaternion.Euler(-55f, 0f, 0f);
            Box("Plate", panel.transform, new Vector3(0f, -0.03f, 0f), new Vector3(0.85f, 0.05f, 0.3f), 0.16f);

            // Слева — настройка манёвра: время узла и три оси характеристической скорости.
            string[] knobs = { "LeverNodeTime", "LeverDeltaVX", "LeverDeltaVY", "LeverDeltaVZ" };
            for (int i = 0; i < knobs.Length; i++)
            {
                Knob(knobs[i], panel.transform, new Vector3(-0.36f + 0.08f * i, 0.03f, 0f));
            }

            Lamp("LampDeltaV", panel.transform, new Vector3(0.04f, 0.015f, -0.1f));
            Lamp("LampPlan", panel.transform, new Vector3(0.16f, 0.015f, -0.1f));
            Button("ButtonBurn", panel.transform, new Vector3(0.04f, 0.02f, 0.03f), new Vector3(0.1f, 0.03f, 0.1f));
            Button("ButtonNew", panel.transform, new Vector3(0.16f, 0.02f, 0.03f), new Vector3(0.1f, 0.03f, 0.1f));

            // Справа — управление самим прибором: куда смотрит камера и как далеко.
            // Крестовина разворота — та же пара осей, что и стрелками на клавиатуре;
            // рычаг дальности рядом с ней, потому что дальность на этом приборе и есть
            // масштаб, а не отдельная настройка.
            Vector3 pad = new(0.28f, 0.02f, 0.02f);
            Button("ViewYawMinus", panel.transform, pad + new Vector3(-0.05f, 0f, 0f), new Vector3(0.04f, 0.02f, 0.04f));
            Button("ViewYawPlus", panel.transform, pad + new Vector3(0.05f, 0f, 0f), new Vector3(0.04f, 0.02f, 0.04f));
            Button("ViewPitchPlus", panel.transform, pad + new Vector3(0f, 0f, 0.05f), new Vector3(0.04f, 0.02f, 0.04f));
            Button("ViewPitchMinus", panel.transform, pad + new Vector3(0f, 0f, -0.05f), new Vector3(0.04f, 0.02f, 0.04f));
            Knob("LeverRange", panel.transform, new Vector3(0.39f, 0.03f, 0.02f));
        }

        /// <summary>
        /// Крутилка — колёсико, торчащее из панели: форма говорит, что её крутят, а не жмут.
        /// Цилиндр положен на бок, ось поперёк панели.
        /// </summary>
        void Knob(string name, Transform parent, Vector3 localPosition)
        {
            GameObject knob = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            knob.name = name;
            knob.transform.SetParent(parent, false);
            knob.transform.localPosition = localPosition;
            knob.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            knob.transform.localScale = new Vector3(0.05f, 0.012f, 0.05f);
            knob.AddComponent<Shade>().value = 0.5f;
            knob.AddComponent<PanelKnob>();
        }

        /// <summary>
        /// Перед креслом: ДУ и РСУ. Этим управляют не глядя, поэтому панель ближе всего
        /// к рукам и лежит почти горизонтально.
        /// </summary>
        void BuildFlightPanel()
        {
            GameObject flight = Box("FlightPanel", transform, new Vector3(0f, 0.78f, 0.62f),
                new Vector3(0.8f, 0.06f, 0.45f), 0.16f);
            flight.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
        }

        /// <summary>
        /// По правую руку ближе к корме: телеметрия корабля и СОЖ. Смотрят на них редко,
        /// а дотягиваться надо не глядя, поэтому ниже экранов и ближе к плечу.
        /// </summary>
        void BuildSystemsPanel()
        {
            GameObject systems = Box("SystemsPanel", transform, new Vector3(0.82f, 0.95f, -0.45f),
                new Vector3(0.06f, 0.5f, 0.5f), 0.16f);
            systems.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
        }

        void Button(string name, Transform parent, Vector3 localPosition, Vector3 size)
            => Box(name, parent, localPosition, size, 0.55f).AddComponent<PanelButton>();

        void Lamp(string name, Transform parent, Vector3 localPosition)
        {
            GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lamp.name = name;
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = localPosition;
            lamp.transform.localScale = new Vector3(0.07f, 0.02f, 0.04f);
            lamp.AddComponent<PanelLamp>();
        }

        void BuildHatch()
        {
            GameObject hatch = Box("Hatch", transform, new Vector3(0f, HatchHeight * 0.5f, -(Length + Wall) * 0.5f),
                new Vector3(HatchWidth, HatchHeight, Wall), 0.34f);
            hatch.AddComponent<PilotSeat>();
            exit = Anchor("Exit", transform, new Vector3(0f, 0.9f, -(Length * 0.5f + 0.8f)), Quaternion.Euler(0f, 180f, 0f));
        }

        static Transform Anchor(string name, Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            GameObject anchor = new(name);
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = localPosition;
            anchor.transform.localRotation = localRotation;
            return anchor.transform;
        }

        static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 size, float shade)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = size;
            box.AddComponent<Shade>().value = shade;
            return box;
        }
    }
}
