using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Проверка панели: находит органы, которые ничем не управляют.
    ///
    /// Всё, что кокпит умеет, переехало в устройства (ShipDevice): двигатель знает про тягу,
    /// навигационный компьютер — про план, вид и ручки. Проводки по имени объекта больше нет,
    /// от неё осталось только это — сторож, который не даёт немому органу молчать.
    ///
    /// Немой орган на панели — обещание, которое некому выполнить. Проверяется в Start:
    /// устройства подключаются в OnEnable, а он у всех объектов сцены проходит раньше любого
    /// Start, так что к этому моменту щиток собран целиком.
    ///
    /// Ищет по всей сцене, а не среди своих детей: щиток общий, орган работает где угодно,
    /// и искать среди детей значило бы молчать ровно в том случае, который надо поймать.
    /// </summary>
    public class ControlsTelemetry : MonoBehaviour
    {
        void Start()
        {
            foreach (PanelButton button in All<PanelButton>()) Check(button, button.port, button.Wired, "Кнопка");
            foreach (PanelKnob knob in All<PanelKnob>()) Check(knob, knob.port, knob.Wired, "Крутилка");
            foreach (PanelSwitcher switcher in All<PanelSwitcher>()) Check(switcher, null, switcher.Wired, "Переключатель");
            foreach (PanelLamp lamp in All<PanelLamp>()) Check(lamp, lamp.port, lamp.Wired, "Лампа");
            foreach (PanelLever lever in All<PanelLever>()) Check(lever, lever.port, lever.Wired, "Рычаг");
            foreach (PanelStick stick in All<PanelStick>()) Check(stick, null, stick.Wired, "Стик");

            foreach (PanelReading reading in All<PanelReading>())
            {
                if (reading.port == null) Debug.LogWarning($"Табло «{reading.name}»: не выбран разъём.", reading);
                else if (!reading.port.Assigned) Debug.LogWarning($"Табло «{reading.name}»: разъём «{reading.port.name}» не привязан — прогоните меню Cockpit → Разъёмы.", reading);
            }
        }

        static T[] All<T>() where T : Component
            => FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        static void Check(Component organ, ControlPort port, bool wired, string kind)
        {
            if (port != null && !port.Assigned)
            {
                Debug.LogWarning($"{kind} «{organ.name}»: разъём «{port.name}» не привязан к делу — " +
                    "прогоните меню Cockpit → Разъёмы.", organ);
            }
            else if (!wired)
            {
                Debug.LogWarning($"{kind} «{organ.name}» ни к чему не подключена.", organ);
            }
        }
    }
}
