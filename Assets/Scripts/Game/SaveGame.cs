using System.Collections.Generic;
using System.IO;
using DoublePrecision;
using Newtonsoft.Json;
using OuterSpace;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// Сохранение в один слот. Мир из эпохи восстанавливается сам — тела и станции идут по
    /// аналитическим орбитам, — поэтому в файле только то, что изменил игрок: корабль, план,
    /// цель, деньги, трюм, часы.
    ///
    /// Загрузка — перезагрузка сцены: мир строится заново из файла системы, как при запуске,
    /// и поверх него кладётся сохранённое. Так не надо уметь откатывать каждый объект сцены.
    ///
    /// Не сохраняются положение рычагов, режим двигателя и тяга: органы кокпита встают как
    /// в сцене, а корабль после загрузки не жжёт.
    /// </summary>
    public static class SaveGame
    {
        const string FileName = "save.json";

        class Data
        {
            public double epoch;
            public string central;
            public OrbitElements orbit;
            public double[] rotation;
            public double[] angularVelocity;
            public ShipOrientation orientation;
            public bool rightPortActive;
            public double methane;
            public double lox;
            public double reactorPower;
            public double reactorHeat;
            public double hullTemperature;
            public double[] wear;
            public double credits;
            public List<CargoData> hold = new();
            public string target;
            public string dockedTo;
            public int dockedPort;
            public double[] dockedOffset;
            public bool latched;
            public string landedOn;
            public double[] landedPosition;
            public double[] landedAttitude;
            public double burnedDeltaV;
            public List<ManeuverData> maneuvers = new();
            public List<TimerData> timers = new();
            public int nextTimer;
            public double stopwatchStart;
        }

        class CargoData
        {
            public string name;
            public double mass;
            public string from;
            public string to;
            public double reward;
            public double deadline;
        }

        class ManeuverData
        {
            public double epoch;
            public double[] deltaV;
            public bool locked;
        }

        class TimerData
        {
            public int number;
            public double epoch;
            public bool fired;
        }

        static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Прочитанное сохранение, которое ждёт, пока сцена построит мир.</summary>
        static Data pending;

        public static bool Exists => File.Exists(FilePath);

        /// <summary>Эпоха, с которой начнётся загружаемая игра, или NaN.</summary>
        public static double PendingEpoch => pending?.epoch ?? double.NaN;

        public static void Save()
        {
            Ship ship = (Ship)SimMono.playerShip;
            GameMono game = GameMono.instance;
            Data data = new()
            {
                epoch = game.Epoch,
                central = ship.centralBody.GameObject.name,
                orbit = ship.orbitParams,
                rotation = Array(ship.attitude.rotation),
                angularVelocity = Array(ship.attitude.angularVelocity),
                orientation = ship.orientation,
                rightPortActive = ship.rightPortActive,
                methane = ship.tanks.methane,
                lox = ship.tanks.lox,
                reactorPower = ship.engine.reactorPower,
                reactorHeat = ship.engine.reactorHeat,
                hullTemperature = ship.hull.temperature,
                wear = System.Array.ConvertAll(ship.hull.panels, panel => panel.wear),
                credits = ship.credits,
                target = SimMono.target?.GameObject.name,
                burnedDeltaV = ship.BurnedDeltaV,
                nextTimer = game.timers.NextNumber,
                stopwatchStart = game.timers.StopwatchStart,
            };
            foreach (Cargo cargo in ship.hold.cargo)
            {
                data.hold.Add(new CargoData
                {
                    name = cargo.name, mass = cargo.mass, from = cargo.from.GameObject.name,
                    to = cargo.to.GameObject.name, reward = cargo.reward, deadline = cargo.deadline,
                });
            }
            if (ship.DockedTo != null)
            {
                data.dockedTo = ship.DockedTo.GameObject.name;
                data.dockedPort = IndexOf(ship.DockedTo.ports, ship.DockedPort);
                data.dockedOffset = Array(ship.DockedOffset);
                data.latched = ship.Latched;
            }
            if (ship.LandedOn != null)
            {
                data.landedOn = ship.LandedOn.GameObject.name;
                data.landedPosition = Array(ship.LandedPosition);
                data.landedAttitude = Array(ship.LandedAttitude);
            }
            foreach (Maneuver maneuver in ship.Maneuvers())
            {
                data.maneuvers.Add(new ManeuverData
                {
                    epoch = maneuver.startEpoch, deltaV = Array(maneuver.deltaLVLHVelocity), locked = maneuver.locked,
                });
            }
            foreach (Timers.Timer timer in game.timers.List)
                data.timers.Add(new TimerData { number = timer.Number, epoch = timer.Epoch, fired = timer.Fired });

            File.WriteAllText(FilePath, JsonConvert.SerializeObject(data, Formatting.Indented));
        }

        public static void Load()
        {
            pending = JsonConvert.DeserializeObject<Data>(File.ReadAllText(FilePath));
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Положить ждущее сохранение на только что построенный мир. Зовёт SimMono.</summary>
        public static void ApplyPending()
        {
            if (pending == null) return;
            Data data = pending;
            pending = null;

            Ship ship = (Ship)SimMono.playerShip;
            ship.SetCentralBody(Find(data.central));
            ship.SetOrbit(data.orbit);
            SimMono.RebuildUpdateOrder();
            SimMono.target = data.target == null ? null : Find(data.target);

            ship.attitude.rotation = Rotation(data.rotation);
            ship.attitude.angularVelocity = Vector(data.angularVelocity);
            ship.orientation = data.orientation;
            ship.rightPortActive = data.rightPortActive;
            ship.tanks.methane = data.methane;
            ship.tanks.lox = data.lox;
            ship.hull.temperature = data.hullTemperature;
            for (int i = 0; i < ship.hull.panels.Length; i++) ship.hull.panels[i].wear = data.wear[i];
            ship.credits = data.credits;
            foreach (CargoData cargo in data.hold)
            {
                ship.hold.cargo.Add(new Cargo
                {
                    name = cargo.name, mass = cargo.mass, from = (Station)Find(cargo.from),
                    to = (Station)Find(cargo.to), reward = cargo.reward, deadline = cargo.deadline,
                });
            }
            if (data.dockedTo != null)
            {
                Station station = (Station)Find(data.dockedTo);
                ship.RestoreDock(station, station.ports[data.dockedPort], Vector(data.dockedOffset), data.latched);
            }
            if (data.landedOn != null)
                ship.RestoreLanding(Find(data.landedOn), Vector(data.landedPosition), Rotation(data.landedAttitude));

            ship.SyncClocks(data.epoch);
            // Реактор остывал бы от нулевой эпохи до нынешней: сначала его часы, потом его тепло.
            ship.engine.UpdateReactor(data.epoch, false);
            ship.engine.reactorPower = data.reactorPower;
            ship.engine.reactorHeat = data.reactorHeat;

            // Узлы по одному и по порядку: каждый следующий строится на траектории предыдущего.
            foreach (ManeuverData saved in data.maneuvers)
            {
                ship.CreateManeuver(saved.epoch - data.epoch);
                Maneuver maneuver = ship.GetManeuver();
                maneuver.deltaLVLHVelocity = Vector(saved.deltaV);
                maneuver.locked = saved.locked;
                maneuver.CalcAndDraw();
            }
            ship.BurnedDeltaV = data.burnedDeltaV;

            List<Timers.Timer> timers = data.timers.ConvertAll(t => new Timers.Timer { Number = t.number, Epoch = t.epoch, Fired = t.fired });
            GameMono.instance.timers.Restore(timers, data.nextTimer, data.stopwatchStart);
        }

        static SpaceObject Find(string name)
        {
            if (SimMono.root.GameObject.name == name) return SimMono.root;
            SpaceObject body = SimMono.bodies.Find(b => b.GameObject.name == name);
            if (body != null) return body;
            return SimMono.stations.Find(s => s.GameObject.name == name);
        }

        static int IndexOf(IReadOnlyList<PortData> ports, PortData port)
        {
            for (int i = 0; i < ports.Count; i++)
                if (ports[i] == port) return i;
            return -1;
        }

        static double[] Array(Vector3d v) => new[] { v.x, v.y, v.z };

        static Vector3d Vector(double[] a) => new(a[0], a[1], a[2]);

        static double[] Array(Quaterniond q) => new[] { q.x, q.y, q.z, q.w };

        static Quaterniond Rotation(double[] a) => new(a[0], a[1], a[2], a[3]);
    }
}
