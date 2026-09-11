using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Цвет заглушечной геометрии. Отдельным компонентом, потому что материал, созданный
    /// кодом в редакторе, не переживает сохранение сцены: он не ассет. Число переживает.
    /// Уедет вместе с кубами, как только появятся настоящие материалы.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class Shade : MonoBehaviour
    {
        [Range(0f, 1f)] public float value = 0.2f;

        void Awake()
        {
            GetComponent<Renderer>().material.color = new Color(value, value, value * 1.05f);
        }
    }
}
