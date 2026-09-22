using System;
using System.Collections.Generic;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Щиток: кто на самом деле стоит за каждой записью перечня. Здесь живут делегаты —
    /// то единственное, что нельзя класть в ассет, потому что ассет переживает выход из Play.
    ///
    /// Пять видов связи, по числу того, что орган умеет сказать и услышать: команда
    /// (сделать), ручка (щёлкнуть в плюс или минус), величина (выставить целиком),
    /// сигнал (да/нет) и показание (число).
    ///
    /// Величина стоит особняком: это единственный случай, когда орган не подталкивает,
    /// а называет значение. Такой у рычага тяги, у которого положение есть физический факт.
    /// Хранит его всё равно устройство — орган только выставляет.
    ///
    /// Ключ — номер из enum, а не ассет: перечень живёт в коде, ассет только указывает на
    /// запись. Поэтому потерянная или пересозданная ссылка на ассет ничего не ломает, пока
    /// номер тот же.
    ///
    /// Органы сюда не записываются и ничего не кешируют: они спрашивают щиток каждый раз,
    /// когда на них навели или нажали. Это снимает вопрос порядка инициализации и делает
    /// иерархию сцены безразличной: орган работает, где бы ни висело его устройство.
    ///
    /// Подключают себя устройства (ShipDevice), каждое своё. Поэтому на одну запись должно
    /// приходиться ровно одно устройство: спор — ошибка сборки, и щиток о ней говорит.
    /// </summary>
    public static class ControlBus
    {
        readonly struct Command
        {
            public readonly Action Run;
            public readonly Func<bool> Available;

            public Command(Action run, Func<bool> available)
            {
                Run = run;
                Available = available;
            }
        }

        readonly struct Step
        {
            public readonly Action<int> Turn;
            public readonly Func<bool> Available;

            public Step(Action<int> turn, Func<bool> available)
            {
                Turn = turn;
                Available = available;
            }
        }

        readonly struct Setting
        {
            public readonly Action<double> Set;
            public readonly Func<bool> Available;

            public Setting(Action<double> set, Func<bool> available)
            {
                Set = set;
                Available = available;
            }
        }

        static readonly Dictionary<CommandId, Command> commands = new();
        static readonly Dictionary<StepId, Step> steps = new();
        static readonly Dictionary<SettingId, Setting> settings = new();
        static readonly Dictionary<SignalId, Func<bool>> signals = new();
        static readonly Dictionary<ReadingId, Func<double>> readings = new();

        /// <summary>
        /// Щиток статичен, а статика переживает выход из Play при выключенном domain reload:
        /// без этого после второго запуска висели бы обработчики от первого. Чистка повешена
        /// на SubsystemRegistration — самую раннюю точку входа в игру, до Awake любого объекта
        /// сцены. Устройств несколько, и порядок их пробуждения не определён, поэтому чистить
        /// в Awake кого-то из них нельзя: затёрло бы подключение соседа.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Clear();

        public static void Clear()
        {
            commands.Clear();
            steps.Clear();
            settings.Clear();
            signals.Clear();
            readings.Clear();
        }

        public static void Bind(CommandId id, Action run, Func<bool> available = null)
        {
            if (!Check(id, id == CommandId.None, commands.ContainsKey(id))) return;
            commands[id] = new Command(run, available);
        }

        public static void Bind(StepId id, Action<int> turn, Func<bool> available = null)
        {
            if (!Check(id, id == StepId.None, steps.ContainsKey(id))) return;
            steps[id] = new Step(turn, available);
        }

        public static void Bind(SettingId id, Action<double> set, Func<bool> available = null)
        {
            if (!Check(id, id == SettingId.None, settings.ContainsKey(id))) return;
            settings[id] = new Setting(set, available);
        }

        public static void Bind(SignalId id, Func<bool> read)
        {
            if (!Check(id, id == SignalId.None, signals.ContainsKey(id))) return;
            signals[id] = read;
        }

        public static void Bind(ReadingId id, Func<double> read)
        {
            if (!Check(id, id == ReadingId.None, readings.ContainsKey(id))) return;
            readings[id] = read;
        }

        static bool Check(object id, bool empty, bool taken)
        {
            if (empty)
            {
                Debug.LogError("ControlBus: попытка подключиться к пустому номеру.");
                return false;
            }
            if (taken)
            {
                Debug.LogError($"ControlBus: за «{id}» взялись двое. Орган должен слушаться одного " +
                    "устройства, иначе непонятно, чем именно ты управляешь.");
            }
            return true;
        }

        public static void Unbind(CommandId id) => commands.Remove(id);

        public static void Unbind(StepId id) => steps.Remove(id);

        public static void Unbind(SettingId id) => settings.Remove(id);

        public static void Unbind(SignalId id) => signals.Remove(id);

        public static void Unbind(ReadingId id) => readings.Remove(id);

        /// <summary>Можно ли сейчас нажать. Запись без устройства недоступна, а не падает.</summary>
        public static bool Available(CommandId id)
        {
            if (!commands.TryGetValue(id, out Command command)) return false;
            return command.Run != null && (command.Available == null || command.Available());
        }

        public static bool Available(StepId id)
        {
            if (!steps.TryGetValue(id, out Step step)) return false;
            return step.Turn != null && (step.Available == null || step.Available());
        }

        public static bool Available(SettingId id)
        {
            if (!settings.TryGetValue(id, out Setting setting)) return false;
            return setting.Set != null && (setting.Available == null || setting.Available());
        }

        public static void Invoke(CommandId id)
        {
            if (commands.TryGetValue(id, out Command command))
            {
                command.Run?.Invoke();
                return;
            }
            Debug.LogWarning($"ControlBus: «{id}» есть в перечне, но ни одно устройство её не взяло.");
        }

        /// <summary>Щелчок ручки: направление +1 или −1, величину решает устройство.</summary>
        public static void Turn(StepId id, int direction)
        {
            if (steps.TryGetValue(id, out Step step))
            {
                step.Turn?.Invoke(direction);
                return;
            }
            Debug.LogWarning($"ControlBus: «{id}» есть в перечне, но ни одно устройство её не взяло.");
        }

        public static void Set(SettingId id, double value)
        {
            if (settings.TryGetValue(id, out Setting setting))
            {
                setting.Set?.Invoke(value);
                return;
            }
            Debug.LogWarning($"ControlBus: «{id}» есть в перечне, но ни одно устройство её не взяло.");
        }

        public static bool Read(SignalId id)
        {
            return signals.TryGetValue(id, out Func<bool> read) && read != null && read();
        }

        /// <summary>Показание в системных единицах. Нет устройства — нет числа.</summary>
        public static bool TryRead(ReadingId id, out double value)
        {
            value = 0.0;
            if (!readings.TryGetValue(id, out Func<double> read) || read == null) return false;
            value = read();
            return true;
        }
    }
}
