using System.IO;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
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
        /// <summary>Запрошенная игроком ступень перемотки.</summary>
        public uint TimeSpeed => TimeToggler.Current;
        /// <summary>Фактическая перемотка: запрошенная, ограниченная ближайшим событием.</summary>
        public double WarpSpeed { get; private set; }
        /// <summary>Чем ограничена перемотка, или null. Без этого ограничение читается как заедающее управление.</summary>
        public string WarpLimitReason { get; private set; }
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
            WarpSpeed = AllowedWarp();
            _epoch += Time.deltaTime * WarpSpeed;
        }
        /// <summary>
        /// Прибор выдаёт min(желаемая, разрешённая): лестница ступеней задаёт только желаемую
        /// скорость, а плавность получается из ограничения по ближайшему событию.
        /// </summary>
        private double AllowedWarp()
        {
            WarpLimitReason = null;
            uint requested = TimeToggler.Current;
            if (SimMono.playerShip is not Ship ship || requested <= 1) return requested;

            Ship.WarpEvent next = ship.NextEvent(_epoch);
            double allowed = next == null ? requested : WarpLimit.Allowed(requested, next.Epoch - _epoch);
            double cap = WarpLimit.FrameCap(ship.NarrowestFlybyWindow(), Time.deltaTime);
            if (cap < allowed)
            {
                WarpLimitReason = "FLYBY WINDOW";
                return cap;
            }
            if (allowed < requested && next != null)
            {
                WarpLimitReason = $"{next.Reason} {TrajectoryRenderer.Clock(next.Epoch - _epoch)}";
            }
            return allowed;
        }
        public GameData LoadGame()
        {
            return SystemLoader.Load(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, SystemFile)));
        }
    }
}
