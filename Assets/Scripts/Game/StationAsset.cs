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
        [Tooltip("Вид станции за окном, в метрах, оси как у корпуса. Пусто — общий образец ExteriorView.")]
        public GameObject view;
    }
}
