using UnityEngine;

namespace Game
{
    public class TimeToggler : MonoBehaviour
    {
        // Шаг «1-2-5» на декаду: числа остаются круглыми, игрок знает, во сколько раз ускорился,
        // и провала между 1× и 100× больше нет — при 1× на трёхчасовой орбите не происходит
        // ничего, при 100× виток пролетает за 108 секунд.
        static readonly uint[] Speeds = { 0, 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000, 100000 };
        // Шестнадцать ступеней перещёлкивать долго, поэтому прыжок на декаду — модификатором.
        public const int StepsPerDecade = 3;

        // Ступень, выше которой не пустит работающий двигатель.
        const int RealTimeIndex = 1;

        private int _index = 1;
        bool locked;

        public static System.Collections.Generic.IReadOnlyList<uint> Ladder => Speeds;
        public uint Current => Speeds[_index];
        public TimeToggler Faster() => Shift(1);
        public TimeToggler Slower() => Shift(-1);

        /// <summary>
        /// Прожиг считается по симуляционному времени, и на перемотке шаг интегрирования
        /// становится длиннее самого прожига. Поэтому на время работы двигателя перемотка
        /// сбрасывается в 1× и выше не поднимается — пауза и 1× остаются доступны.
        /// </summary>
        public void SetLocked(bool locked)
        {
            this.locked = locked;
            if (locked && _index > RealTimeIndex) Shift(RealTimeIndex - _index);
        }

        public TimeToggler Shift(int steps)
        {
            _index = Mathf.Clamp(_index + steps, 0, locked ? RealTimeIndex : Speeds.Length - 1);
            OnTimeChange();
            return this;
        }
        private void OnTimeChange()
        {
            Debug.Log($"Time speed changed to x{Current}");
        }
    }
}
