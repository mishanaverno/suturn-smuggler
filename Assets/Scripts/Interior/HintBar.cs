using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Interior
{
    /// <summary>
    /// Строка подсказок внизу экрана, как статусная строка Blender: какие наборы клавиш
    /// держат стики и что сделают кнопки мыши и колесо с органом под курсором. Пересобирается
    /// только когда меняется набор подсказок.
    ///
    /// Клавиши — иконки: каждое имя ниже — класс hint-bar__icon--имя в HintBar.uss, где и
    /// указана картинка.
    /// </summary>
    public class HintBar : MonoBehaviour
    {
        static readonly string[][] setKeys =
        {
            new[] { "w", "s", "a", "d", "q", "e" },
            new[] { "i", "k", "j", "l", "u", "o" },
        };
        static readonly string[] setNames = { "WASDQE", "IJKLUO" };
        static readonly string[] setButtons = { "mouse-left", "mouse-right" };

        [Tooltip("Документ с разметкой HintBar.uxml.")]
        public UIDocument document;

        VisualElement root;
        readonly List<(string[] keys, string action)> hints = new();
        string shown;

        void Awake()
        {
            if (document != null) return;
            Debug.LogError($"HintBar на «{name}»: не указан UIDocument с разметкой подсказок.", this);
            enabled = false;
        }

        // Разметку UIDocument собирает в своём OnEnable, порядок которого с нашим не задан.
        void Start() => root = document.rootVisualElement.Q("root");

        void Update()
        {
            if (root == null) return;
            hints.Clear();
            if (PilotMono.instance != null && PilotMono.instance.Active) Cockpit(PilotMono.instance.hand);
            else if (PlayerMono.instance != null && PlayerMono.instance.Active) Feet(PlayerMono.instance.hand);

            string signature = string.Join("\n", hints.Select(hint => string.Join(" ", hint.keys) + "\t" + hint.action));
            if (signature == shown) return;
            shown = signature;
            root.Clear();
            foreach ((string[] keys, string action) in hints)
            {
                var item = new VisualElement();
                item.AddToClassList("hint-bar__item");
                foreach (string key in keys)
                {
                    var icon = new VisualElement();
                    icon.AddToClassList("hint-bar__icon");
                    icon.AddToClassList("hint-bar__icon--" + key);
                    item.Add(icon);
                }
                var actionLabel = new Label(action);
                actionLabel.AddToClassList("hint-bar__action");
                item.Add(actionLabel);
                root.Add(item);
            }
        }

        void Cockpit(Interactor hand)
        {
            IInteractable aimed = hand != null ? hand.Aimed : null;
            for (int set = 0; set < setKeys.Length; set++)
            {
                StickControl held = StickControl.Holder(set);
                if (held != null) hints.Add((setKeys[set], held.title));
            }
            if (aimed is StickControl stick)
            {
                for (int set = 0; set < setKeys.Length; set++)
                {
                    string verb = StickControl.Holder(set) == stick ? "Release" : "Take on";
                    hints.Add((new[] { setButtons[set] }, $"{verb} {setNames[set]}"));
                }
            }
            else
            {
                if (aimed is IDraggable) hints.Add((new[] { "mouse-left" }, "Drag"));
                else if (aimed != null) hints.Add((new[] { "mouse-left" }, "Press"));
                hints.Add((new[] { "mouse-right" }, "Hold to look around"));
            }
            hints.Add((new[] { "mouse-scroll" }, aimed is IScrollable ? "Turn" : "Zoom"));
            hints.Add((new[] { "arrows" }, "Turn head"));
            if (!ControlBus.Read(SignalId.EngineRunning)) hints.Add((new[] { "escape" }, "Stand up"));
        }

        void Feet(Interactor hand)
        {
            hints.Add((new[] { "w", "a", "s", "d" }, "Move"));
            if (hand != null && hand.Aimed != null) hints.Add((new[] { "e" }, hand.Aimed.Prompt));
            hints.Add((new[] { "escape" }, "Menu"));
        }
    }
}
