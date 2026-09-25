using DoublePrecision;
using Game;
using OuterSpace.Sim.Objects;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OuterSpace.Sim
{
    public class SimMono : MonoBehaviour
    {
        public static SimMono instance;
        public static SpaceObject root { get; private set; }
        public static List<SpaceObject> bodies { get; private set; } = new();
        public static SpaceObject playerShip { get; private set; }
        // Цель прицеливания: объект, к которому игрок сводит траекторию. Своей сферы влияния
        // у неё может и не быть — это точка встречи, а не будущее центральное тело.
        public static SpaceObject target;
        // Порядок обновления по глубине иерархии: SetRELATIVE_* переводит относительные
        // величины в глобальные через текущее состояние родителя, поэтому родитель
        // обязан обновиться раньше ребёнка в том же тике.
        public static List<SpaceObject> updateOrder { get; private set; } = new();

        void Awake()
        {
            instance = this;
        }
        private void Start()
        {
            CreateSim(GameMono.instance.gameData);
        }
        private void CreateSim(GameData data)
        {
            // Списки статические и переживают выгрузку сцены.
            bodies.Clear();
            updateOrder.Clear();
            target = null;

            // Порядок в data.system.objects задан загрузчиком: родитель всегда раньше ребёнка.
            Dictionary<string, SpaceObject> byId = new();
            foreach (ObjectData objData in data.system.objects)
            {
                if (objData.orbit == null)
                {
                    root = Place(new RootBody(objData.gm, Prefab(objData)), objData);
                    root.SetVelocity(Vector3d.zero);
                    byId.Add(objData.id, root);
                    continue;
                }
                CelestialBody body = Place(new CelestialBody(objData.gm, Prefab(objData)), objData);
                body.SetCentralBody(byId[objData.parent]);
                body.SetOrbit(ToElements(objData.orbit, body.centralBody.MU));
                byId.Add(objData.id, body);
                bodies.Add(body);
            }

            PlayerShip shipData = data.system.playerShip;
            playerShip = Place(new Ship(shipData.dryMass, shipData.methane, shipData.lox, shipData.propulsion, Prefab(shipData)), shipData);
            playerShip.SetCentralBody(byId[shipData.parent]);
            playerShip.SetOrbit(ToElements(shipData.orbit, playerShip.centralBody.MU));

            RebuildUpdateOrder();
        }

        private static GameObject Prefab(ObjectData data) => ResourcesLoader.LoadPrefab($"Bodies/{data.simPrefab}");

        private T Place<T>(T obj, ObjectData data) where T : SpaceObject
        {
            obj.GameObject.name = data.name;
            obj.GameObject.transform.parent = transform;
            obj.radius = data.radius;
            obj.knowledge = data.knowledge;
            return obj;
        }

        public static OrbitElements ToElements(OrbitData orbit, double mu) => new()
        {
            semiMajorAxis = orbit.semiMajorAxis,
            eccentricity = orbit.eccentricity,
            inclination = orbit.inclination,
            longitudeOfAscendingNode = orbit.longitudeOfAscendingNode,
            argumentOfPeriapsis = orbit.argumentOfPeriapsis,
            meanAnomalyAtEpoch = orbit.meanAnomalyAtEpoch,
            startEpoch = orbit.epoch,
            mu = mu
        };

        public static void RebuildUpdateOrder()
        {
            List<SpaceObject> all = new() { root };
            all.AddRange(bodies);
            all.Add(playerShip);
            updateOrder = all.OrderBy(Depth).ToList();
        }

        private static int Depth(SpaceObject obj)
        {
            int depth = 0;
            for (SpaceObject central = obj.centralBody; central != null; central = central.centralBody)
            {
                depth++;
            }
            return depth;
        }

        private void FixedUpdate()
        {
            for (int i = 0; i < updateOrder.Count; i++)
            {
                updateOrder[i].FixedUpdate();
            }

            // Переходы — отдельным проходом: кандидаты в центральные тела и старое тело, от
            // которого Reframe сдвигает манёвр, должны быть уже на этом тике, а не на прошлом.
            bool orderChanged = false;
            for (int i = 0; i < updateOrder.Count; i++)
            {
                SpaceObject obj = updateOrder[i];
                if (!obj.TracksSOITransitions) continue;

                SpaceObject central = SOITransition.ResolveCentralBody(obj, obj.centralBody, bodies, SOITransition.Hysteresis);
                if (central == obj.centralBody) continue;

                SOITransition.ChangeCentralBody(obj, central, GameMono.instance.Epoch);
                orderChanged = true;
            }
            if (orderChanged) RebuildUpdateOrder();
        }
    }
}
