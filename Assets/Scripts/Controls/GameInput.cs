using UnityEngine;
using UnityEngine.InputSystem;

namespace Controls
{
    /// <summary>
    /// Единственное место, где нажатие превращается в намерение. До этого ввод читали сами
    /// модели (Ship, TimeToggler, NavDisplayMono), и раскладка была рассыпана по слоям — узнать,
    /// что делает клавиша, можно было только грепом.
    ///
    /// Карта собирается кодом, а не .inputactions-ассетом, по той же причине, по которой
    /// навигационный экран строит свою камеру кодом: описание рядом с объяснением, а не в
    /// бинарнике, который читается только редактором.
    ///
    /// Контексты не «режимы интерфейса», а разные занятия. Тело летает по кораблю в
    /// невесомости: WASD — тяга скафандра, курсор захвачен, взгляд идёт за мышью. Пилот на своём месте не ходит вовсе: курсор
    /// видим и указывает на органы управления, голова доворачивается краями экрана, и тот же
    /// WASD означает три оси характеристической скорости. Одна клавиша не может значить два
    /// намерения одновременно, поэтому карты взаимно исключающие, а не приоритетные.
    /// </summary>
    public static class GameInput
    {
        public enum Context { None, Bridge, Cockpit }

        static InputActionAsset asset;
        static InputActionMap bridge;
        static InputActionMap cockpit;

        public static Context Current { get; private set; } = Context.None;

        // --- корабль: игрок как тело ---
        public static InputAction Move { get; private set; }
        public static InputAction Look { get; private set; }
        public static InputAction Vertical { get; private set; }
        public static InputAction Sprint { get; private set; }
        /// <summary>Гашение скорости двигателями ориентации. В пустоте иначе не остановиться.</summary>
        public static InputAction Brake { get; private set; }
        public static InputAction Interact { get; private set; }

        // --- кокпит: игрок как пилот ---
        /// <summary>Уйти с места пилота и снова встать на ноги.</summary>
        public static InputAction Leave { get; private set; }
        /// <summary>Положение курсора в пикселях окна. Не смещение: курсор виден и осмыслен.</summary>
        public static InputAction Point { get; private set; }
        /// <summary>Нажатие на орган управления под курсором.</summary>
        public static InputAction Click { get; private set; }
        /// <summary>Колесо: им крутят крутилку, на которую наведён курсор.</summary>
        public static InputAction Scroll { get; private set; }
        /// <summary>Поворот головы. Клавишами, а не мышью: мышь занята курсором.</summary>
        public static InputAction View { get; private set; }
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
        /// <summary>
        /// Десять режимов ориентации — десять действий, а не перебор по кругу: перебор из
        /// десяти пунктов хуже десяти клавиш. Индекс соответствует цифре на клавиатуре,
        /// нулевой элемент — клавиша 1.
        /// </summary>
        public static InputAction[] Orientation { get; private set; }

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

            cockpit = asset.AddActionMap("Cockpit");
            Leave = cockpit.AddAction("Leave", InputActionType.Button, "<Keyboard>/escape");
            Point = cockpit.AddAction("Point", InputActionType.Value, "<Mouse>/position");
            Click = cockpit.AddAction("Click", InputActionType.Button, "<Mouse>/leftButton");
            Scroll = cockpit.AddAction("Scroll", InputActionType.Value, "<Mouse>/scroll/y");
            View = cockpit.AddAction("View", InputActionType.Value);
            View.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            Thrust = cockpit.AddAction("Thrust", InputActionType.Button, "<Keyboard>/space");
            ManeuverCreate = cockpit.AddAction("ManeuverCreate", InputActionType.Button, "<Keyboard>/m");
            ManeuverDelete = cockpit.AddAction("ManeuverDelete", InputActionType.Button, "<Keyboard>/r");
            Coarse = cockpit.AddAction("Coarse", InputActionType.Button, "<Keyboard>/leftShift");
            Fine = cockpit.AddAction("Fine", InputActionType.Button, "<Keyboard>/leftCtrl");
            CycleFocus = cockpit.AddAction("CycleFocus", InputActionType.Button, "<Keyboard>/tab");
            CycleTarget = cockpit.AddAction("CycleTarget", InputActionType.Button, "<Keyboard>/t");
            TimeWarp = Axis(cockpit, "TimeWarp", "<Keyboard>/comma", "<Keyboard>/period");
            Decade = cockpit.AddAction("Decade", InputActionType.Button, "<Keyboard>/leftShift");
            ObjectInfo = cockpit.AddAction("ObjectInfo", InputActionType.Button, "<Keyboard>/i");

            string[] digits = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };
            Orientation = new InputAction[digits.Length];
            for (int i = 0; i < digits.Length; i++)
            {
                Orientation[i] = cockpit.AddAction($"Orientation{digits[i]}", InputActionType.Button,
                    $"<Keyboard>/{digits[i]}");
            }
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
            if (context == Context.Bridge) bridge.Enable();
            if (context == Context.Cockpit) cockpit.Enable();
            Current = context;
        }

        /// <summary>Ввод глохнет целиком — на паузе, в меню, во время катсцены.</summary>
        public static void Release() => Switch(Context.None);
    }
}
