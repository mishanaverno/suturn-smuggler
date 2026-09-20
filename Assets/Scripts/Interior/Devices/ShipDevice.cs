using System;
using System.Collections.Generic;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Устройство корабля: то, чем орган на панели в конце концов управляет. Двигатель,
    /// навигационный компьютер, СОЖ — у каждого свой кусок перечня дел и никакого знания
    /// о том, где стоят кнопки и есть ли они вообще.
    ///
    /// Зачем разделено. Единая проводка на весь кокпит росла бы вместе с кораблём и рано или
    /// поздно стала бы списком всего, что игра умеет, — читать его перестанут. Устройство же
    /// отвечает на понятный вопрос: «что умеет двигатель». Оно живёт в сцене на своём объекте
    /// (можно на самом агрегате), его видно и можно выключить.
    ///
    /// Кнопка при этом не привязана к устройству: она держит разъём, разъём указывает на дело,
    /// дело взяло на себя устройство. Поэтому кнопка создания манёвра управляет навигацией,
    /// кнопка прожига — двигателем, и обе остаются одинаковыми кубиками на одной панели.
    ///
    /// Подключение — на OnEnable, снятие — на OnDisable. Выключенное устройство уносит свои
    /// дела со щитка, и органы, которые ими управляли, гаснут сами: на них больше нельзя
    /// навестись. Это не побочный эффект, а то, как должен выглядеть снятый или обесточенный
    /// агрегат.
    /// </summary>
    public abstract class ShipDevice : MonoBehaviour
    {
        readonly List<CommandId> boundCommands = new();
        readonly List<StepId> boundSteps = new();
        readonly List<SignalId> boundSignals = new();
        readonly List<ReadingId> boundReadings = new();

        /// <summary>
        /// Устройство на месте, но работает ли. Сюда позже сядут питание, поломка и ремонт:
        /// неработающее устройство не отвечает на команды и не даёт показаний, а органы при
        /// нём остаются — мёртвая кнопка на панели честнее исчезнувшей.
        /// </summary>
        public virtual bool Operational => true;

        protected static Ship Ship => SimMono.playerShip as Ship;

        protected static NavDisplayPanel Nav => NavDisplayPanel.instance;

        /// <summary>Какие дела устройство берёт на себя. Вызывается при включении.</summary>
        protected abstract void Wire();

        void OnEnable()
        {
            Forget();
            Wire();
        }

        void OnDisable()
        {
            foreach (CommandId id in boundCommands) ControlBus.Unbind(id);
            foreach (StepId id in boundSteps) ControlBus.Unbind(id);
            foreach (SignalId id in boundSignals) ControlBus.Unbind(id);
            foreach (ReadingId id in boundReadings) ControlBus.Unbind(id);
            Forget();
        }

        void Forget()
        {
            boundCommands.Clear();
            boundSteps.Clear();
            boundSignals.Clear();
            boundReadings.Clear();
        }

        /// <summary>
        /// Доступность дела всегда умножается на исправность устройства, чтобы каждому
        /// устройству не приходилось помнить об этом в каждой строке.
        /// </summary>
        protected void Bind(CommandId id, Action run, Func<bool> available = null)
        {
            ControlBus.Bind(id, run, () => Operational && (available == null || available()));
            boundCommands.Add(id);
        }

        protected void Bind(StepId id, Action<int> turn, Func<bool> available = null)
        {
            ControlBus.Bind(id, turn, () => Operational && (available == null || available()));
            boundSteps.Add(id);
        }

        protected void Bind(SignalId id, Func<bool> read)
        {
            ControlBus.Bind(id, () => Operational && read());
            boundSignals.Add(id);
        }

        /// <summary>
        /// Показание отдаётся в системных единицах — метрах, секундах, метрах в секунду.
        /// Как его напишут человеку, решает разъём: множитель, единица и знаки после запятой
        /// лежат в ассете, потому что это оформление, а не физика.
        ///
        /// Неисправное устройство показаний не даёт вовсе — табло гаснет, а не врёт нулём.
        /// </summary>
        protected void Bind(ReadingId id, Func<double> read)
        {
            ControlBus.Bind(id, () => Operational ? read() : double.NaN);
            boundReadings.Add(id);
        }
    }
}
