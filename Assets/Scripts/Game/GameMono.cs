using DoublePrecision;
using OuterSpace;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class GameMono : MonoBehaviour
    {
        public double _epoch = 0;
        public double Epoch => _epoch;
        public TimeToggler TimeToggler;
        public static GameMono instance;
        public GameData gameData { get; private set; }
        public KeyValuePair<TimeToggler.TimeSpeed, uint> TimeSpeed => TimeToggler.Current;
        void Awake()
        {
            TimeToggler = GetComponent<TimeToggler>();
            instance = this;
            gameData = LoadGame();
            _epoch = gameData.startEpoch;
        }
        public void Update()
        {
            Tick();
        }
        private void Tick()
        {
            _epoch += Time.deltaTime * TimeToggler.Current.Value;
        }
        public GameData LoadGame()
        {
            ObjectData sun = new();
            sun.name = "Sun";
            sun.description = "desc";
            sun.velocity = Vector3d.zero;
            sun.position = Vector3d.zero;
            sun.rotation = Vector3d.zero;
            sun.simPrefab = "Star";
            sun.mass = 19000000000 * Constants.simMassMultiplier;

            ObjectData planet1 = new();
            planet1.name = "Earth";
            planet1.description = "desc";
            planet1.velocity = new(0, 0, 29788);
            planet1.position = new(149.6 * Constants.simDistanceMultiplier, 0, 0);
            planet1.rotation = Vector3d.zero;
            planet1.simPrefab = "Planet";
            planet1.mass = 1900000 * Constants.simMassMultiplier;

            ObjectData planet2 = new();
            planet2.name = "Moon";
            planet2.description = "desc";
            planet2.velocity = new(0, 0, 2100);
            planet2.position = new(152.376 * Constants.simDistanceMultiplier, 0, 0);
            planet2.rotation = Vector3d.zero;
            planet2.simPrefab = "Planet";
            planet2.mass = 190000 * Constants.simMassMultiplier;

            GameData gameData = new();
            gameData.startEpoch = Time.time;
            gameData.system = new();
            gameData.system.star = sun;
            gameData.system.objects = new()
            {
                planet1,
                planet2
            };

            PlayerShip playerShip = new PlayerShip();
            playerShip.name = "player";
            playerShip.simPrefab = "PlayerShip";
            playerShip.position = new((152.756) * Constants.simDistanceMultiplier, 0, 0);
            playerShip.rotation = Vector3d.zero;
            playerShip.velocity = new(0,0, 2000);
            playerShip.mass = 100000;
            gameData.system.playerShip = playerShip;
            return gameData;
        }
    }
}
