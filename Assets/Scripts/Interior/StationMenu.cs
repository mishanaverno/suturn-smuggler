using System.Globalization;
using System.Linq;
using Controls;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Interior
{
    /// <summary>
    /// Меню станции: всё, что делается у неё, пристыкованным, — заправка и расстыковка.
    /// Открывается само, когда узел механически зафиксирован, закрытое открывается снова
    /// клавишей из кабины. Пока
    /// меню открыто, кабина ввода не слышит: щелчок по кнопке меню не должен заодно
    /// нажимать орган под ней.
    /// </summary>
    public class StationMenu : MonoBehaviour
    {
        const string Hidden = "station-menu--hidden";
        const string RowHidden = "station-menu__row--hidden";

        [Tooltip("Документ с разметкой StationMenu.uxml.")]
        public UIDocument document;

        VisualElement root;
        Label stationName;
        VisualElement methaneRow;
        VisualElement loxRow;
        Label methaneLevel;
        Label loxLevel;
        Button methaneFill;
        Button loxFill;

        Station shown;
        Station lastDocked;
        GameInput.Context resume;
        CursorLockMode resumeLock;
        bool resumeVisible;

        static Ship Ship => SimMono.playerShip as Ship;

        void Awake()
        {
            if (document != null) return;
            Debug.LogError($"StationMenu на «{name}»: не указан UIDocument с разметкой меню.", this);
            enabled = false;
        }

        // Разметку UIDocument собирает в своём OnEnable, порядок которого с нашим не задан.
        void Start()
        {
            VisualElement tree = document.rootVisualElement;
            root = tree.Q("root");
            stationName = tree.Q<Label>("stationName");
            methaneRow = tree.Q("methaneRow");
            loxRow = tree.Q("loxRow");
            methaneLevel = tree.Q<Label>("methaneLevel");
            loxLevel = tree.Q<Label>("loxLevel");
            methaneFill = tree.Q<Button>("methaneFill");
            loxFill = tree.Q<Button>("loxFill");
            methaneFill.clicked += () => Ship.tanks.Refill(Ship.tanks.methaneCapacity - Ship.tanks.methane, 0.0);
            loxFill.clicked += () => Ship.tanks.Refill(0.0, Ship.tanks.loxCapacity - Ship.tanks.lox);
            tree.Q<Button>("undockButton").clicked += () =>
            {
                Close();
                Ship.Undock();
            };
            tree.Q<Button>("closeButton").clicked += Close;
        }

        void Update()
        {
            Ship ship = Ship;
            if (ship == null || root == null) return;

            Station docked = ship.Latched ? ship.DockedTo : null;
            if (docked != lastDocked)
            {
                lastDocked = docked;
                if (docked != null) Open(docked);
                else Close();
            }
            else if (shown == null && docked != null && GameInput.StationMenu.WasPressedThisFrame()) Open(docked);

            if (shown != null) Refresh(ship);
        }

        void Open(Station station)
        {
            if (shown != null) return;
            shown = station;
            resume = GameInput.Current;
            resumeLock = Cursor.lockState;
            resumeVisible = Cursor.visible;
            GameInput.Release();
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;

            stationName.text = station.GameObject.name.ToUpperInvariant();
            methaneRow.EnableInClassList(RowHidden, !station.sells.Contains("methane"));
            loxRow.EnableInClassList(RowHidden, !station.sells.Contains("lox"));
            root.RemoveFromClassList(Hidden);
        }

        void Close()
        {
            if (shown == null) return;
            shown = null;
            root.AddToClassList(Hidden);
            GameInput.Switch(resume);
            Cursor.lockState = resumeLock;
            Cursor.visible = resumeVisible;
        }

        void Refresh(Ship ship)
        {
            Tanks tanks = ship.tanks;
            methaneLevel.text = Level(tanks.methane, tanks.methaneCapacity);
            loxLevel.text = Level(tanks.lox, tanks.loxCapacity);
            methaneFill.SetEnabled(tanks.methane < tanks.methaneCapacity);
            loxFill.SetEnabled(tanks.lox < tanks.loxCapacity);
        }

        static string Level(double amount, double capacity) =>
            string.Format(CultureInfo.InvariantCulture, "{0,6:F0} / {1:F0} kg", amount, capacity);
    }
}
