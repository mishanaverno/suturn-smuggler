using DoublePrecision;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class GameData
    {
        public double startEpoch;
        public SystemData system;
    }
    public class SystemData
    {
        public ObjectData star;
        public List<ObjectData> objects;
        public PlayerShip playerShip;

    }
    public class ObjectData
    {
        public string name;
        public string description;
        public string simPrefab;
        public double mass;
        public Vector3d position;
        public Vector3d velocity;
        public Vector3d rotation;
    }
    public class SpaceShip : ObjectData
    {

    }
    public class PlayerShip : SpaceShip
    {

    }
}
