using System;
using Controls;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Стик: рукоять с тремя осями — от себя/на себя, вбок и поворот. Мышью его не двигают:
    /// двойным щелчком его берут набором клавиш, и дальше он отклоняется, пока клавиши
    /// нажаты. Левая кнопка берёт первым набором (WS, AD, QE), правая — вторым (IK, JL, UO).
    ///
    /// Набор держит только один стик сразу: взяли им другой — прежний отпущен и встаёт
    /// в середину. Иначе одна клавиша значила бы два намерения. Стик же может держаться
    /// обоими наборами — тогда отклонения складываются.
    ///
    /// Разъёмы у осей — величины: стик называет отклонение целиком, от −1 до 1, а что им
    /// крутить, решает устройство. Отдаёт он его только при изменении — соседний орган на той
    /// же величине не должен каждый кадр перебиваться стоящим стиком.
    ///
    /// Трансформ стик двигает сам, как рычаг: отклонение и есть величина.
    /// </summary>
    public class PanelStick : MonoBehaviour, IInteractable
    {
        const int Axes = 3;

        static readonly PanelStick[] holders = new PanelStick[2];

        [Tooltip("Величины осей по порядку: от себя/на себя, вбок, поворот рукояти. Пустой разъём — ось не работает.")]
        public SettingPort[] ports = new SettingPort[Axes];

        [Tooltip("Наклон при полном отклонении, градусов. От себя — вокруг местной X, вбок — вокруг местной Z.")]
        public float tilt = 20f;

        [Tooltip("Поворот рукояти при полном отклонении, градусов, вокруг местной Y.")]
        public float twist = 25f;

        readonly float[] deflection = new float[Axes];
        Quaternion home;

        void Awake() => home = transform.localRotation;

        void OnValidate()
        {
            if (ports.Length != Axes) Array.Resize(ref ports, Axes);
        }

        void OnDisable()
        {
            for (int set = 0; set < holders.Length; set++)
            {
                if (holders[set] == this) holders[set] = null;
            }
            Deflect(new float[Axes]);
        }

        public string Prompt
        {
            get
            {
                string held = Held(0) && Held(1) ? " [LMB+RMB]" : Held(0) ? " [LMB]" : Held(1) ? " [RMB]" : "";
                return name + held;
            }
        }

        public bool Available => Array.Exists(ports, port => port != null && ControlBus.Available(port.id));

        public bool Wired => Array.Exists(ports, port => port != null && port.Assigned);

        /// <summary>Одиночный щелчок ничего не значит: стик берут двойным.</summary>
        public void Interact() { }

        /// <summary>Взять стик набором клавиш. Прежний стик этого набора отпускается.</summary>
        public void Take(int set)
        {
            PanelStick previous = holders[set];
            holders[set] = this;
            if (previous != null && previous != this) previous.Deflect(previous.Read());
        }

        bool Held(int set) => holders[set] == this;

        void Update()
        {
            if (!Held(0) && !Held(1)) return;
            Deflect(Read());
        }

        float[] Read()
        {
            float[] input = new float[Axes];
            for (int set = 0; set < holders.Length; set++)
            {
                if (!Held(set)) continue;
                for (int axis = 0; axis < Axes; axis++) input[axis] += GameInput.Stick[set][axis].ReadValue<float>();
            }
            for (int axis = 0; axis < Axes; axis++) input[axis] = Mathf.Clamp(input[axis], -1f, 1f);
            return input;
        }

        void Deflect(float[] input)
        {
            bool moved = false;
            for (int axis = 0; axis < Axes; axis++)
            {
                if (input[axis] == deflection[axis]) continue;
                deflection[axis] = input[axis];
                moved = true;
                SettingPort port = ports[axis];
                if (port != null && port.Assigned && ControlBus.Available(port.id)) ControlBus.Set(port.id, input[axis]);
            }
            if (!moved) return;
            transform.localRotation = home * Quaternion.Euler(deflection[0] * tilt, deflection[2] * twist, -deflection[1] * tilt);
        }
    }
}
