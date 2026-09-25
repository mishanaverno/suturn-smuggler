using UnityEngine;

namespace OuterSpace.Sim
{
    /// <summary>
    /// Материал всех линий прибора: unlit, с вершинным цветом, без проверки глубины.
    ///
    /// Материал тел для этого не годится — он освещаемый и вершинный цвет игнорирует, а линии
    /// на приборе не освещаются ничем: они не предметы, а показания. По той же причине они не
    /// должны отсекаться телами: диск Титана закрывал собой траекторию корабля именно там,
    /// где она нужнее всего, — у поверхности, при подходе и стыковке.
    ///
    /// Материал собирается в коде, а не лежит ассетом: так шейдер и его настройки не разъедутся
    /// при переимпорте, и видно, из чего материал сделан.
    /// </summary>
    public static class SimLine
    {
        const string ShaderPath = "Shaders/InstrumentLine";
        const string FallbackMaterialPath = "Materials/Line";

        // Не ??=: оно не видит уничтоженный Unity-объект, а материал из прошлой play-сессии
        // уничтожается при выходе из неё, хотя статическое поле его всё ещё держит.
        public static Material Material => material != null ? material : material = Create();
        static Material material;

        static Material Create()
        {
            Shader shader = Resources.Load<Shader>(ShaderPath);
            if (shader != null) return new Material(shader) { name = "InstrumentLine" };

            Debug.LogWarning($"SimLine: нет шейдера {ShaderPath}, линии будут отсекаться телами.");
            return Resources.Load<Material>(FallbackMaterialPath);
        }
    }
}
