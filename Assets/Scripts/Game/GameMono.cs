using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Game
{
    public class GameMono : MonoBehaviour
    {
        public const string SystemFile = "systems/saturn.json";
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
            return SystemLoader.Load(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, SystemFile)));
        }
    }
}
