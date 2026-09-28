using System;
using System.Globalization;
using System.Linq;
using Controls;
using Game;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Interior
{
    /// <summary>
    /// Меню станции: всё, что делается у неё, пристыкованным, — заправка, грузы и расстыковка.
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
        Label credits;
        VisualElement methaneRow;
        VisualElement loxRow;
        Label methaneLevel;
        Label loxLevel;
        Button methaneFill;
        Button loxFill;
        VisualElement repairSection;
        Label repairCost;
        Button repairButton;
        Label holdLevel;
        VisualElement holdList;
        VisualElement offerList;

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
            credits = tree.Q<Label>("credits");
            methaneRow = tree.Q("methaneRow");
            loxRow = tree.Q("loxRow");
            methaneLevel = tree.Q<Label>("methaneLevel");
            loxLevel = tree.Q<Label>("loxLevel");
            methaneFill = tree.Q<Button>("methaneFill");
            loxFill = tree.Q<Button>("loxFill");
            repairSection = tree.Q("repairSection");
            repairCost = tree.Q<Label>("repairCost");
            repairButton = tree.Q<Button>("repairButton");
            repairButton.clicked += () => Ship.RepairHull(shown.repair);
            holdLevel = tree.Q<Label>("holdLevel");
            holdList = tree.Q("holdList");
            offerList = tree.Q("offerList");
            methaneFill.clicked += () => Ship.BuyFuel(Price("methane"));
            loxFill.clicked += () => Ship.BuyFuel(Price("lox"));
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
                if (docked != null)
                {
                    docked.offers = ContractBoard.Generate(docked, SimMono.stations, GameMono.instance.Epoch);
                    Open(docked);
                }
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
            methaneRow.EnableInClassList(RowHidden, Price("methane") == null);
            loxRow.EnableInClassList(RowHidden, Price("lox") == null);
            repairSection.EnableInClassList(RowHidden, station.repair <= 0.0);
            Rebuild();
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

        FuelPrice Price(string good) => shown.fuel.FirstOrDefault(fuel => fuel.good == good);

        void Refresh(Ship ship)
        {
            Tanks tanks = ship.tanks;
            credits.text = string.Format(CultureInfo.InvariantCulture, "{0:N0} cr", Math.Floor(ship.credits));
            methaneLevel.text = Level(tanks.methane, tanks.methaneCapacity, Price("methane"));
            loxLevel.text = Level(tanks.lox, tanks.loxCapacity, Price("lox"));
            methaneFill.SetEnabled(CanBuy(ship, tanks.methane < tanks.methaneCapacity, Price("methane")));
            loxFill.SetEnabled(CanBuy(ship, tanks.lox < tanks.loxCapacity, Price("lox")));
            double cost = ship.HullRepairCost(shown.repair);
            repairCost.text = string.Format(CultureInfo.InvariantCulture, "{0:N0} cr", Math.Ceiling(cost));
            repairButton.SetEnabled(cost > 0.0 && ship.credits > 0.0);
            holdLevel.text = string.Format(CultureInfo.InvariantCulture, "{0,6:F0} / {1:F0} kg", ship.hold.Mass, ship.hold.capacity);
        }

        void Rebuild()
        {
            Ship ship = Ship;
            double epoch = GameMono.instance.Epoch;
            holdList.Clear();
            foreach (Cargo cargo in ship.hold.cargo)
                holdList.Add(Row(CargoText(cargo, epoch), "DELIVER", cargo.to == shown, () =>
                {
                    ship.Deliver(cargo, GameMono.instance.Epoch);
                    Rebuild();
                }));
            offerList.Clear();
            foreach (Cargo cargo in shown.offers)
                offerList.Add(Row(CargoText(cargo, epoch), "TAKE", ship.CanTake(cargo), () =>
                {
                    ship.Take(cargo);
                    Rebuild();
                }));
        }

        static VisualElement Row(string text, string action, bool enabled, Action clicked)
        {
            VisualElement row = new();
            row.AddToClassList("station-menu__row");
            Label label = new(text);
            label.AddToClassList("station-menu__value");
            Button button = new(clicked) { text = action };
            button.AddToClassList("station-menu__button");
            button.SetEnabled(enabled);
            row.Add(label);
            row.Add(button);
            return row;
        }

        static string CargoText(Cargo cargo, double epoch)
        {
            double days = (cargo.deadline - epoch) / 86400.0;
            string due = days >= 0.0 ? string.Format(CultureInfo.InvariantCulture, "{0:F1} d", days) : "LATE";
            return string.Format(CultureInfo.InvariantCulture, "{0} {1:F1} t TO {2}\n{3:N0} cr, {4}",
                cargo.name, cargo.mass / 1000.0, cargo.to.GameObject.name.ToUpperInvariant(), cargo.reward, due);
        }

        static bool CanBuy(Ship ship, bool room, FuelPrice price) =>
            price != null && room && (price.price <= 0.0 || ship.credits > 0.0);

        static string Level(double amount, double capacity, FuelPrice price) =>
            string.Format(CultureInfo.InvariantCulture, "{0,6:F0} / {1:F0} kg  {2:F0} cr/kg", amount, capacity, price?.price ?? 0.0);
    }
}
