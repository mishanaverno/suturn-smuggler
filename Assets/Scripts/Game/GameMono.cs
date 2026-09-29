using System.IO;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using UnityEngine;

namespace Game
{
    // Раньше всех: SimMono строит мир в своём Awake из данных, загруженных здесь.
    [DefaultExecutionOrder(-100)]
    public class GameMono : MonoBehaviour
    {
        public const string SystemFile = "systems/saturn.json";
        public double _epoch = 0;
        public double Epoch => _epoch;
        public TimeToggler TimeToggler;
        public static GameMono instance;
        public GameData gameData { get; private set; }
        public readonly Timers timers = new();
        readonly System.Collections.Generic.List<Schedule.Entry> schedule = new();
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
            _epoch = double.IsNaN(SaveGame.PendingEpoch) ? gameData.startEpoch : SaveGame.PendingEpoch;
        }
        public void Update()
        {
            Tick();
        }
        private void Tick()
        {
            WarpSpeed = AllowedWarp();
            _epoch += Time.deltaTime * WarpSpeed;
            // Таймер ставят, чтобы не проспать на перемотке, поэтому сработавший её снимает.
            if (timers.Fire(_epoch)) TimeToggler.RealTime();
        }
        /// <summary>
        /// За WarpLimit.GuardSeconds до ближайшей строки расписания (Schedule — то же, что на
        /// экране часов) перемотка сбрасывается в 1×, и включать её обратно — дело игрока.
        /// Никакой лестницы ступеней и никаких страховок сверх этого: одно правило, которое
        /// игрок может держать в голове.
        /// </summary>
        private double AllowedWarp()
        {
            WarpLimitReason = null;
            uint requested = TimeToggler.Current;
            if (requested <= 1) return requested;

            Schedule.Entry? next = Schedule.Next(SimMono.playerShip as Ship, timers, _epoch, schedule);
            if (next == null) return requested;

            double toEvent = next.Value.Epoch - _epoch;
            if (toEvent <= WarpLimit.GuardSeconds)
            {
                TimeToggler.RealTime();
                return 1.0;
            }
            double allowed = WarpLimit.Allowed(requested, toEvent, Time.deltaTime);
            if (allowed < requested)
            {
                WarpLimitReason = $"{next.Value.Label} {TrajectoryRenderer.Clock(toEvent)}";
            }
            return allowed;
        }

        public GameData LoadGame()
        {
            return SystemLoader.Load(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, SystemFile)));
        }
    }
}
