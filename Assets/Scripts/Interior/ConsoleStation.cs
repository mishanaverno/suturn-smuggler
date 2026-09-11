using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Навигационный пульт: пока — только плоскость, на которую садится экран прибора.
    /// Отдельным компонентом, а не полем в BridgeMono, потому что пультов в кокпите будет
    /// больше одного, и каждый принесёт свои поверхности и органы управления.
    /// </summary>
    public class ConsoleStation : MonoBehaviour
    {
        /// <summary>Плоскость, на которую садится холст навигационного экрана.</summary>
        public Transform screen;
        /// <summary>Малый экран контроля орбиты.</summary>
        public ReadoutPanel orbit;
        /// <summary>Малый экран списка целей.</summary>
        public ReadoutPanel targets;
    }
}
