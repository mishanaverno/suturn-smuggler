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
            foreach (ButtonControl button in All<ButtonControl>()) Check(button, button.port, button.Wired, button.part, "Кнопка");
            foreach (ScreenSelectButton button in All<ScreenSelectButton>()) Check(button, null, button.Wired, button.part, "Кнопка экрана");
            foreach (KnobControl knob in All<KnobControl>()) Check(knob, knob.port, knob.Wired, knob.part, "Крутилка");
            foreach (SwitcherControl switcher in All<SwitcherControl>()) Check(switcher, null, switcher.Wired, switcher.part, "Переключатель");
            foreach (LeverControl lever in All<LeverControl>()) Check(lever, lever.port, lever.Wired, lever.part, "Рычаг");
            foreach (StickControl stick in All<StickControl>()) Check(stick, null, stick.Wired, stick.part, "Стик");
            foreach (LampIndicator lamp in All<LampIndicator>()) Check(lamp, lamp.port, lamp.Wired, lamp.part, "Лампа");
            foreach (GaugeIndicator gauge in All<GaugeIndicator>()) CheckReading(gauge, gauge.port, gauge.part, "Прибор");
            foreach (ReadoutIndicator readout in All<ReadoutIndicator>()) CheckReading(readout, readout.port, readout.part, "Табло");
        }

        static void CheckReading(Component organ, ReadingPort port, Object part, string kind)
        {
            if (port == null) Debug.LogWarning($"{kind} «{organ.name}»: не выбран разъём.", organ);
            else if (!port.Assigned) Debug.LogWarning($"{kind} «{organ.name}»: разъём «{port.name}» не привязан — прогоните меню Cockpit → Разъёмы.", organ);
            if (part == null) Debug.LogError($"{kind} «{organ.name}»: не задана деталь (part).", organ);
        }

        static T[] All<T>() where T : Component
            => FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        static void Check(Component organ, ControlPort port, bool wired, Object part, string kind)
        {
            if (part == null) Debug.LogError($"{kind} «{organ.name}»: не задана деталь (part).", organ);
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
