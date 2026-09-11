using UnityEngine;

namespace OuterSpace.Sim
{
    /// <summary>
    /// Материал всех линий прибора: unlit, с вершинным цветом. Материал тел для этого не годится —
    /// он освещаемый и вершинный цвет игнорирует, а линии на приборе не освещаются ничем: они
    /// не предметы, а показания.
    /// </summary>
    public static class SimLine
    {
        public static Material Material => material ??= Resources.Load<Material>("Materials/Line");
        static Material material;
    }
}
