using UnityEngine;

namespace Game
{
    /// <summary>
    /// Станция отдельным ассетом: файл системы только перечисляет их, иначе он разросся бы
    /// до нечитаемого. Лежит в Resources — загрузчик находит её по пути из файла системы.
    /// </summary>
    [CreateAssetMenu(menuName = "Suturn/Station")]
    public class StationAsset : ScriptableObject
    {
        public StationData station;
        [Tooltip("Модель станции за окном, в метрах, оси как у корпуса. Стыковочные узлы — дочерние объекты с именем на Port (Port, Port.1…): синяя ось наружу из узла, зелёная — его верх.")]
        public GameObject view;
    }
}
