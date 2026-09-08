using NUnit.Framework;
using OuterSpace.Sim;
using UnityEngine;

/// <summary>
/// Материал линий написан руками, а ошибка в нём не падает: линии просто теряют цвет
/// или становятся розовыми.
/// </summary>
public class SimLineTest
{
    [Test]
    public void LineMaterialIsUnlitAndTakesVertexColor()
    {
        Material material = SimLine.Material;

        Assert.IsNotNull(material, "материал линий не загрузился из Resources");
        Assert.IsNotNull(material.shader, "у материала линий нет шейдера");
        Assert.AreEqual("Sprites/Default", material.shader.name);
    }
}
