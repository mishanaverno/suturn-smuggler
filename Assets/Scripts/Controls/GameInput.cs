using UnityEngine;
using UnityEngine.InputSystem;

namespace Controls
{
    /// <summary>
    /// Единственное место, где нажатие превращается в намерение. До этого ввод читали сами
    /// модели (Ship, TimeToggler, NavDisplayPanel), и раскладка была рассыпана по слоям — узнать,
    /// что делает клавиша, можно было только грепом.
    ///
    /// Карта собирается кодом, а не .inputactions-ассетом, по той же причине, по которой
    /// навигационный экран строит свою камеру кодом: описание рядом с объяснением, а не в
    /// бинарнике, который читается только редактором.
    ///
    /// Контексты не «режимы интерфейса», а разные занятия. Тело летает по кораблю в
    /// невесомости: WASD — тяга скафандра, курсор захвачен, взгляд идёт за мышью. Пилот на своём месте не ходит вовсе: курсор
    /// видим и указывает на органы управления, а WASD с Q/E разворачивает корабль — это его
    /// основное занятие, поэтому главные клавиши отданы ему, а голова ушла на стрелки. Одна
    /// клавиша не может значить два намерения одновременно, поэтому карты взаимно
    /// исключающие, а не приоритетные.
    /// </summary>
    public static class GameInput
    {
        public enum Context { None, Bridge, Cockpit, FreeCamera, Menu }

        static InputActionAsset asset;
        static InputActionMap bridge;
        static InputActionMap cockpit;
        // Перемотка нужна и пилоту, и свободной камере: смотреть на затмение без перемотки нечего.
        static InputActionMap time;
        static InputActionMap freeCamera;
        static InputActionMap menu;
        // Отладка включена всегда, в любом занятии.
        static InputActionMap debug;

        public static Context Current { get; private set; } = Context.None;

        // --- корабль: игрок как тело ---
        public static InputAction Move { get; private set; }
        public static InputAction Look { get; private set; }
        public static InputAction Vertical { get; private set; }
        public static InputAction Sprint { get; private set; }
        /// <summary>Гашение скорости двигателями ориентации. В пустоте иначе не остановиться.</summary>
        public static InputAction Brake { get; private set; }
        public static InputAction Interact { get; private set; }
        /// <summary>
        /// Меню игры — только на ногах: в кресле Esc встаёт с места, и одна клавиша не может
        /// значить два намерения.
        /// </summary>
        public static InputAction PauseMenu { get; private set; }

        // --- меню игры ---
        public static InputAction MenuBack { get; private set; }

        // --- кокпит: игрок как пилот ---
        /// <summary>Уйти с места пилота и снова встать на ноги.</summary>
        public static InputAction Leave { get; private set; }
        /// <summary>Положение курсора в пикселях окна. Не смещение: курсор виден и осмыслен.</summary>
        public static InputAction Point { get; private set; }
        /// <summary>Нажатие на орган управления под курсором.</summary>
        public static InputAction Click { get; private set; }
        /// <summary>Колесо: крутилку под курсором крутит, мимо крутилки — меняет увеличение.</summary>
        public static InputAction Scroll { get; private set; }
        /// <summary>Поворот головы. Клавишами, а не мышью: мышь занята курсором.</summary>
        public static InputAction View { get; private set; }
        /// <summary>
        /// Правая кнопка: по стику — взять его вторым набором клавиш, мимо стика — пока зажата,
        /// голову ведёт мышь (Glance). Левая (Click) по стику берёт первым набором.
        /// </summary>
        public static InputAction Focus { get; private set; }
        /// <summary>Движение мыши, пока голову ведут правой кнопкой.</summary>
        public static InputAction Glance { get; private set; }
        /// <summary>
        /// Два набора по три оси: WS, AD, QE и IK, JL, UO. Набор ничего не вращает сам —
        /// он отклоняет тот стик, которым его взяли, а что делает стик, решают его разъёмы.
        /// </summary>
        public static InputAction[][] Stick { get; private set; }
        public static InputAction Thrust { get; private set; }
        public static InputAction ManeuverCreate { get; private set; }
        public static InputAction ManeuverDelete { get; private set; }
        // Характеристическая скорость, узел по времени, дальность и разворот вида живут на
        // физической панели кокпита (см. CockpitControls) и клавиш не имеют вовсе: орган
        // управления должен быть один, иначе непонятно, чем именно ты сейчас управляешь.
        public static InputAction Coarse { get; private set; }
        public static InputAction Fine { get; private set; }
        public static InputAction CycleFocus { get; private set; }
        public static InputAction CycleTarget { get; private set; }
        public static InputAction TimeWarp { get; private set; }
        public static InputAction Decade { get; private set; }
        public static InputAction ObjectInfo { get; private set; }
        /// <summary>Меню станции, у которой корабль стоит. Открывается и само — при захвате.</summary>
        public static InputAction StationMenu { get; private set; }
        /// <summary>
        /// Десять режимов ориентации — десять действий, а не перебор по кругу: перебор из
        /// десяти пунктов хуже десяти клавиш. Индекс соответствует цифре на клавиатуре,
        /// нулевой элемент — клавиша 1.
        /// </summary>
        public static InputAction[] Orientation { get; private set; }

        // --- отладка: свободная камера ---
        public static InputAction FreeCameraToggle { get; private set; }
        public static InputAction FreeMove { get; private set; }
        public static InputAction FreeVertical { get; private set; }
        public static InputAction FreeRoll { get; private set; }
        public static InputAction FreeLook { get; private set; }
        /// <summary>Колесо меняет скорость: от метров до сотен тысяч километров в секунду.</summary>
        public static InputAction FreeSpeed { get; private set; }
        public static InputAction FreeFast { get; private set; }

        public static void Initialize()
        {
            if (asset != null) return;

            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "SuturnControls";

            bridge = asset.AddActionMap("Bridge");
            Move = bridge.AddAction("Move", InputActionType.Value);
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            Vertical = Axis(bridge, "Vertical", "<Keyboard>/leftCtrl", "<Keyboard>/space");
            Look = bridge.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            Brake = bridge.AddAction("Brake", InputActionType.Button, "<Keyboard>/c");
            Sprint = bridge.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            Interact = bridge.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            PauseMenu = bridge.AddAction("PauseMenu", InputActionType.Button, "<Keyboard>/escape");

            menu = asset.AddActionMap("Menu");
            MenuBack = menu.AddAction("MenuBack", InputActionType.Button, "<Keyboard>/escape");

            cockpit = asset.AddActionMap("Cockpit");
            Leave = cockpit.AddAction("Leave", InputActionType.Button, "<Keyboard>/escape");
            Point = cockpit.AddAction("Point", InputActionType.Value, "<Mouse>/position");
            Click = cockpit.AddAction("Click", InputActionType.Button, "<Mouse>/leftButton");
            Scroll = cockpit.AddAction("Scroll", InputActionType.Value, "<Mouse>/scroll/y");
            View = cockpit.AddAction("View", InputActionType.Value);
            View.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            Focus = cockpit.AddAction("Focus", InputActionType.Button, "<Mouse>/rightButton");
            Glance = cockpit.AddAction("Glance", InputActionType.Value, "<Mouse>/delta");
            Stick = new[]
            {
                new[]
                {
                    Axis(cockpit, "Stick1Axis1", "<Keyboard>/s", "<Keyboard>/w"),
                    Axis(cockpit, "Stick1Axis2", "<Keyboard>/a", "<Keyboard>/d"),
                    Axis(cockpit, "Stick1Axis3", "<Keyboard>/q", "<Keyboard>/e"),
                },
                new[]
                {
                    Axis(cockpit, "Stick2Axis1", "<Keyboard>/k", "<Keyboard>/i"),
                    Axis(cockpit, "Stick2Axis2", "<Keyboard>/j", "<Keyboard>/l"),
                    Axis(cockpit, "Stick2Axis3", "<Keyboard>/u", "<Keyboard>/o"),
                },
            };
            Thrust = cockpit.AddAction("Thrust", InputActionType.Button, "<Keyboard>/space");
            ManeuverCreate = cockpit.AddAction("ManeuverCreate", InputActionType.Button, "<Keyboard>/m");
            ManeuverDelete = cockpit.AddAction("ManeuverDelete", InputActionType.Button, "<Keyboard>/r");
            Coarse = cockpit.AddAction("Coarse", InputActionType.Button, "<Keyboard>/leftShift");
            Fine = cockpit.AddAction("Fine", InputActionType.Button, "<Keyboard>/leftCtrl");
            CycleFocus = cockpit.AddAction("CycleFocus", InputActionType.Button, "<Keyboard>/tab");
            CycleTarget = cockpit.AddAction("CycleTarget", InputActionType.Button, "<Keyboard>/t");
            ObjectInfo = cockpit.AddAction("ObjectInfo", InputActionType.Button, "<Keyboard>/p");
            StationMenu = cockpit.AddAction("StationMenu", InputActionType.Button, "<Keyboard>/b");

            string[] digits = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };
            Orientation = new InputAction[digits.Length];
            for (int i = 0; i < digits.Length; i++)
            {
                Orientation[i] = cockpit.AddAction($"Orientation{digits[i]}", InputActionType.Button,
                    $"<Keyboard>/{digits[i]}");
            }

            time = asset.AddActionMap("Time");
            TimeWarp = Axis(time, "TimeWarp", "<Keyboard>/comma", "<Keyboard>/period");
            Decade = time.AddAction("Decade", InputActionType.Button, "<Keyboard>/leftShift");

            freeCamera = asset.AddActionMap("FreeCamera");
            FreeMove = freeCamera.AddAction("FreeMove", InputActionType.Value);
            FreeMove.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            FreeVertical = Axis(freeCamera, "FreeVertical", "<Keyboard>/leftCtrl", "<Keyboard>/space");
            FreeRoll = Axis(freeCamera, "FreeRoll", "<Keyboard>/e", "<Keyboard>/q");
            FreeLook = freeCamera.AddAction("FreeLook", InputActionType.Value, "<Mouse>/delta");
            FreeSpeed = freeCamera.AddAction("FreeSpeed", InputActionType.Value, "<Mouse>/scroll/y");
            FreeFast = freeCamera.AddAction("FreeFast", InputActionType.Button, "<Keyboard>/leftShift");

            debug = asset.AddActionMap("Debug");
            FreeCameraToggle = debug.AddAction("FreeCameraToggle", InputActionType.Button, "<Keyboard>/f1");
            // На Mac F1 без fn — яркость экрана и до игры не доходит.
            FreeCameraToggle.AddBinding("<Keyboard>/backquote");
            debug.Enable();
        }

        static InputAction Axis(InputActionMap map, string name, string negative, string positive)
        {
            InputAction action = map.AddAction(name, InputActionType.Value);
            action.AddCompositeBinding("1DAxis")
                .With("Negative", negative)
                .With("Positive", positive);
            return action;
        }

        public static void Switch(Context context)
        {
            Initialize();
            if (Current == context) return;
            bridge.Disable();
            cockpit.Disable();
            time.Disable();
            freeCamera.Disable();
            menu.Disable();
            if (context == Context.Bridge) bridge.Enable();
            if (context == Context.Menu) menu.Enable();
            if (context == Context.Cockpit) cockpit.Enable();
            if (context == Context.FreeCamera) freeCamera.Enable();
            if (context is Context.Cockpit or Context.FreeCamera) time.Enable();
            Current = context;
        }

        /// <summary>Ввод глохнет целиком — на паузе, в меню, во время катсцены.</summary>
        public static void Release() => Switch(Context.None);
    }
}
