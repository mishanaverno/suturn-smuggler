using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Ручка: щелчок в плюс или в минус. Сюда садится крутилка.
    ///
    /// Величины здесь нет и не будет: орган подталкивает, а не выставляет. Насколько сдвинуть
    /// за щелчок, знает только устройство — шаг по Δv равен проценту текущей орбитальной
    /// скорости и зависит ещё и от модификатора «грубо/точно».
    /// </summary>
    [CreateAssetMenu(menuName = "Cockpit/Port/Step", fileName = "Step Port")]
    public sealed class StepPort : ControlPort
    {
        [Tooltip("Какую ручку крутит. Проставляется генератором разъёмов, руками трогать незачем.")]
        public StepId id = StepId.None;

        public override bool Assigned => id != StepId.None;
    }
}
