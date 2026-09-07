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
        public static SpaceObject star { get; private set; }
        public static List<SpaceObject> bodies { get; private set; } = new();
        public static SpaceObject playerShip { get; private set; }
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

            star = new Star(data.system.star.position, data.system.star.mass, ResourcesLoader.LoadPrefab($"Bodies/{data.system.star.simPrefab}"));
            star.GameObject.name = data.system.star.name;
            star.GameObject.transform.parent = transform;
            star.SetVelocity(data.system.star.velocity);

            foreach (ObjectData objData in data.system.objects.OrderByDescending(o => o.mass))
            {
                CelestialBody obj = new(objData.position, objData.mass, ResourcesLoader.LoadPrefab($"Bodies/{objData.simPrefab}"));
                obj.GameObject.name = objData.name;
                obj.GameObject.transform.parent = transform;
                foreach (SpaceObject body in bodies)
                {
                    if (Vector3d.Distance(body.simTransform.GLOBAL_R, obj.simTransform.GLOBAL_R) < body.SOI)
                    {
                        obj.SetCentralBody(body);
                        break;
                    }
                }
                if (obj.centralBody == null)
                {
                    obj.SetCentralBody(star);
                }
                obj.SetVelocity(objData.velocity);
                bodies.Add(obj);

            }

            playerShip = new Ship(data.system.playerShip.position, data.system.playerShip.mass,ResourcesLoader.LoadPrefab($"Bodies/{data.system.playerShip.simPrefab}"));
            playerShip.GameObject.name = data.system.playerShip.name;
            playerShip.GameObject.transform.parent = transform;
            List<SpaceObject> reversed = new (bodies);
            reversed.Reverse();
            foreach (SpaceObject body in reversed)
            {
                if (Vector3d.Distance(body.simTransform.GLOBAL_R, playerShip.simTransform.GLOBAL_R) < body.SOI)
                {
                    playerShip.SetCentralBody(body);
                    break;
                }
            }
            if (playerShip.centralBody == null)
            {
                playerShip.SetCentralBody(star);
            }
            playerShip.SetVelocity(data.system.playerShip.velocity);

            RebuildUpdateOrder();
        }

        public static void RebuildUpdateOrder()
        {
            List<SpaceObject> all = new() { star };
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
            bool orderChanged = false;
            for (int i = 0; i < updateOrder.Count; i++)
            {
                SpaceObject obj = updateOrder[i];
                obj.FixedUpdate();
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