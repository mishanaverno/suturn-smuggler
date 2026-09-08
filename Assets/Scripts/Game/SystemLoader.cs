using DoublePrecision;
using Newtonsoft.Json;
using OuterSpace.Sim;
using System;
using System.Collections.Generic;

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
            if (ship.mass <= 0)
                throw new SystemDataException($"Корабль {ship.id}: масса должна быть положительной, получено {ship.mass}");

            foreach (ObjectData obj in data.system.objects)
            {
                ToRadians(obj.orbit);
                obj.knowledge = KnowledgeSource.Database;
            }
            ToRadians(ship.orbit);
            ship.knowledge = KnowledgeSource.Database;
            return data;
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
