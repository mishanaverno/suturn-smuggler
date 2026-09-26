using DoublePrecision;
using Newtonsoft.Json;
using OuterSpace.Sim;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class SystemDataException : Exception
    {
        public SystemDataException(string message) : base(message) { }
    }

    /// <summary>
    /// Разбор файла системы. Ошибка в одной цифре здесь не падает, а тихо делает мир
    /// неправильным, поэтому всё, что можно проверить при загрузке, проверяется здесь.
    /// </summary>
    public static class SystemLoader
    {
        public static GameData Load(string json)
        {
            GameData data;
            try
            {
                data = JsonConvert.DeserializeObject<GameData>(json);
            }
            catch (JsonException e)
            {
                throw new SystemDataException($"Файл системы не разбирается как JSON: {e.Message}");
            }
            if (data == null) throw new SystemDataException("Файл системы пуст");
            if (data.system == null) throw new SystemDataException("В файле системы нет секции system");
            if (data.system.objects == null || data.system.objects.Count == 0)
                throw new SystemDataException("В файле системы нет ни одного тела");

            Dictionary<string, ObjectData> byId = new();
            foreach (ObjectData obj in data.system.objects)
            {
                ValidateBody(obj);
                if (byId.ContainsKey(obj.id))
                    throw new SystemDataException($"Идентификатор тела повторяется: {obj.id}");
                byId.Add(obj.id, obj);
            }
            data.system.objects = OrderFromRoot(data.system.objects, byId);

            PlayerShip ship = data.system.playerShip;
            if (ship == null) throw new SystemDataException("В файле системы нет корабля игрока");
            ValidateOrbiting(ship, byId);
            if (ship.dryMass <= 0)
                throw new SystemDataException($"Корабль {ship.id}: сухая масса должна быть положительной, получено {ship.dryMass}");
            if (ship.methane < 0 || ship.lox < 0)
                throw new SystemDataException($"Корабль {ship.id}: запасы топлива не могут быть отрицательными");
            if (ship.methane > ship.methaneCapacity || ship.lox > ship.loxCapacity)
                throw new SystemDataException($"Корабль {ship.id}: начальный запас больше ёмкости бака");
            ValidateEngine(ship, "nuclear", ship.propulsion?.nuclear);
            ValidateEngine(ship, "nuclearLox", ship.propulsion?.nuclearLox);
            ValidateEngine(ship, "chemical", ship.propulsion?.chemical);
            ValidateEngine(ship, "rcs", ship.propulsion?.rcs);
            Reactor reactor = ship.propulsion.reactor;
            if (reactor == null || reactor.spoolTime <= 0 || reactor.idleTemperature <= 0 || reactor.fullTemperature < reactor.idleTemperature)
                throw new SystemDataException($"Корабль {ship.id}: у реактора должны быть положительные время выхода на мощность и температуры, полная не ниже холостой");

            data.system.stations = new();
            HashSet<string> stationIds = new();
            foreach (string path in data.system.stationAssets ?? new())
            {
                StationAsset asset = Resources.Load<StationAsset>(path);
                if (asset == null) throw new SystemDataException($"Ассет станции не найден в Resources: {path}");
                // Копия: загрузчик переводит градусы в радианы и нормирует оси узла, а в
                // редакторе это записалось бы в сам ассет.
                StationData station = JsonUtility.FromJson<StationData>(JsonUtility.ToJson(asset.station));
                station.view = asset.view;
                if (asset.view == null) throw new SystemDataException($"Станция {station.id}: не задана модель (view)");
                station.ports = PortsFrom(asset.view);
                if (station.ports.Count == 0) throw new SystemDataException($"Станция {station.id}: в модели {asset.view.name} нет ни одного объекта Port — узлу негде стоять");
                data.system.stations.Add(station);

                if (string.IsNullOrEmpty(station.id)) throw new SystemDataException("У одной из станций пустой id");
                if (byId.ContainsKey(station.id) || !stationIds.Add(station.id))
                    throw new SystemDataException($"Идентификатор станции повторяется: {station.id}");
                ValidateOrbiting(station, byId);
                if (station.radius <= 0)
                    throw new SystemDataException($"Станция {station.id}: габарит должен быть положительным, получено {station.radius}");
                foreach (PortData port in station.ports) ValidatePort(station.id, port);
                if (station.beacon == null || station.beacon.range <= 0 || station.beacon.cone <= 0 || station.beacon.cone >= 90)
                    throw new SystemDataException($"Станция {station.id}: у маяка должны быть положительная дальность и полуугол конуса меньше 90°");
                CaptureData capture = station.capture;
                if (capture == null || capture.range <= 0 || capture.lateral <= 0 || capture.speed <= 0
                    || capture.roll <= 0 || capture.pitch <= 0 || capture.yaw <= 0)
                    throw new SystemDataException($"Станция {station.id}: все допуски захвата должны быть положительными");
                foreach (string good in station.sells)
                    if (good != "methane" && good != "lox")
                        throw new SystemDataException($"Станция {station.id}: неизвестный товар {good}");
            }

            foreach (ObjectData obj in data.system.objects)
            {
                ToRadians(obj.orbit);
                obj.knowledge = KnowledgeSource.Database;
            }
            foreach (StationData station in data.system.stations)
            {
                ToRadians(station.orbit);
                station.knowledge = KnowledgeSource.Database;
            }
            ToRadians(ship.orbit);
            ship.knowledge = KnowledgeSource.Database;
            return data;
        }

        static void ValidateEngine(PlayerShip ship, string name, Engine engine)
        {
            if (engine == null) throw new SystemDataException($"Корабль {ship.id}: нет режима {name}");
            if (engine.thrust <= 0 || engine.isp <= 0 || engine.oxidizerRatio < 0)
                throw new SystemDataException($"Корабль {ship.id}, режим {name}: тяга и удельный импульс должны быть положительными, O/F — неотрицательным");
        }

        static void ToRadians(OrbitData orbit)
        {
            if (orbit == null) return;
            orbit.meanAnomalyAtEpoch *= Mathd.Deg2Rad;
        }

        static void ValidateBody(ObjectData obj)
        {
            if (string.IsNullOrEmpty(obj.id)) throw new SystemDataException("У одного из тел пустой id");
            if (obj.gm <= 0)
                throw new SystemDataException($"Тело {obj.id}: GM должен быть положительным, получено {obj.gm}");
            if (obj.radius <= 0)
                throw new SystemDataException($"Тело {obj.id}: радиус должен быть положительным, получено {obj.radius}");
            if (string.IsNullOrEmpty(obj.parent))
            {
                if (obj.orbit != null) throw new SystemDataException($"Тело {obj.id}: у корня системы не может быть орбиты");
                return;
            }
            ValidateOrbit(obj.id, obj.orbit);
        }

        static void ValidateOrbiting(ObjectData obj, Dictionary<string, ObjectData> byId)
        {
            if (string.IsNullOrEmpty(obj.id)) throw new SystemDataException("У корабля игрока пустой id");
            if (string.IsNullOrEmpty(obj.parent)) throw new SystemDataException($"Объект {obj.id}: не задано центральное тело");
            if (!byId.ContainsKey(obj.parent))
                throw new SystemDataException($"Объект {obj.id}: центральное тело {obj.parent} не найдено");
            ValidateOrbit(obj.id, obj.orbit);
        }

        /// <summary>
        /// Узлы станции с её модели: дочерние объекты с именем на Port, синяя ось — наружу,
        /// зелёная — верх. Узлы в модели и в данных иначе расходились бы при каждой смене
        /// модели.
        ///
        /// Модель в осях корпуса (Z — ось X симуляции, Y — ось Z, X — против оси Y), а
        /// поворот корня модели ExteriorView заменяет своим, поэтому узел меряется от корня
        /// без его поворота, но с масштабом.
        /// </summary>
        public static List<PortData> PortsFrom(GameObject view)
        {
            Transform root = view.transform;
            List<PortData> ports = new();
            foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
            {
                if (part == root || !part.name.StartsWith("Port")) continue;
                ports.Add(PortFrom(
                    Vector3.Scale(root.localScale, root.InverseTransformPoint(part.position)),
                    root.InverseTransformDirection(part.forward),
                    root.InverseTransformDirection(part.up)));
            }
            return ports;
        }

        /// <summary>
        /// Узел по положению и осям в осях корпуса — так устроены и модели станций, и кабина:
        /// Z — ось X симуляции, Y — ось Z, X — против оси Y.
        /// </summary>
        public static PortData PortFrom(Vector3 position, Vector3 axis, Vector3 up) => new()
        {
            position = FromHull(position),
            axis = FromHull(axis).normalized,
            up = FromHull(up).normalized,
        };

        static Vector3d FromHull(Vector3 v) => new(v.z, -v.x, v.y);

        /// <summary>Ось и верх нормируются здесь: по ним считаются углы стыковки.</summary>
        static void ValidatePort(string id, PortData port)
        {
            if (port == null) throw new SystemDataException($"Объект {id}: не задан стыковочный узел");
            if (port.axis.sqrMagnitude <= 0 || port.up.sqrMagnitude <= 0)
                throw new SystemDataException($"Объект {id}: у узла должны быть заданы ось и верх");
            port.axis = port.axis.normalized;
            port.up = port.up.normalized;
            if (Mathd.Abs(Vector3d.Dot(port.axis, port.up)) > 1e-6)
                throw new SystemDataException($"Объект {id}: верх узла не перпендикулярен его оси");
        }

        static void ValidateOrbit(string id, OrbitData orbit)
        {
            if (orbit == null) throw new SystemDataException($"Объект {id}: не задана орбита");
            if (orbit.semiMajorAxis <= 0)
                throw new SystemDataException($"Объект {id}: большая полуось должна быть положительной, получено {orbit.semiMajorAxis}");
            if (orbit.eccentricity < 0 || orbit.eccentricity >= 1)
                throw new SystemDataException($"Объект {id}: эксцентриситет вне [0, 1), получено {orbit.eccentricity}");
            if (orbit.inclination < 0 || orbit.inclination > 180)
                throw new SystemDataException($"Объект {id}: наклонение вне [0, 180], получено {orbit.inclination}");
        }

        /// <summary>Обход от корня вниз: родитель в списке всегда раньше ребёнка.</summary>
        static List<ObjectData> OrderFromRoot(List<ObjectData> objects, Dictionary<string, ObjectData> byId)
        {
            List<ObjectData> roots = objects.FindAll(o => string.IsNullOrEmpty(o.parent));
            if (roots.Count != 1)
                throw new SystemDataException($"В системе должен быть ровно один корень, найдено {roots.Count}");

            Dictionary<string, List<ObjectData>> children = new();
            foreach (ObjectData obj in objects)
            {
                if (string.IsNullOrEmpty(obj.parent)) continue;
                if (!byId.ContainsKey(obj.parent))
                    throw new SystemDataException($"Тело {obj.id}: центральное тело {obj.parent} не найдено");
                if (!children.TryGetValue(obj.parent, out List<ObjectData> siblings))
                    children[obj.parent] = siblings = new();
                siblings.Add(obj);
            }

            List<ObjectData> ordered = new();
            Queue<ObjectData> queue = new();
            queue.Enqueue(roots[0]);
            while (queue.Count > 0)
            {
                ObjectData obj = queue.Dequeue();
                ordered.Add(obj);
                if (!children.TryGetValue(obj.id, out List<ObjectData> siblings)) continue;
                foreach (ObjectData child in siblings) queue.Enqueue(child);
            }
            if (ordered.Count != objects.Count)
            {
                // От корня достижимы не все тела: остальные замкнуты в цикл.
                HashSet<string> reached = new();
                foreach (ObjectData obj in ordered) reached.Add(obj.id);
                List<string> lost = new();
                foreach (ObjectData obj in objects) if (!reached.Contains(obj.id)) lost.Add(obj.id);
                throw new SystemDataException($"Цикл в иерархии тел: {string.Join(", ", lost)}");
            }
            return ordered;
        }
    }
}
