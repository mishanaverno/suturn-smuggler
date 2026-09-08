using DoublePrecision;
using UnityEngine;

namespace OuterSpace.Sim
{
    /// <summary>
    /// Единственное место, где симуляция встречается со сценой. Расчёт ведётся в правой
    /// системе координат с Z вверх — так задают орбиты и так устроен AstroDynamic; Unity
    /// рисует в левой системе с Y вверх. Перестановка Y и Z даёт орбиты в плоскости XZ
    /// и сохраняет направление обхода: прямое движение видно против часовой стрелки сверху.
    ///
    /// Начало отсчёта и масштаб задаёт прибор (NavDisplayMono), а не мир: масштаб — это
    /// положение ручки дальности, а не свойство системы Сатурна.
    /// </summary>
    public static class SimView
    {
        public static Vector3d origin = Vector3d.zero;
        public static double metersPerSceneUnit = NavScale.MetersPerSceneUnit(NavDisplayMono.DefaultRange);

        public static Vector3 ToScene(Vector3d simVector)
        {
            Vector3d scaled = (simVector - origin) / metersPerSceneUnit;
            return new Vector3((float)scaled.x, (float)scaled.z, (float)scaled.y);
        }
    }
}
