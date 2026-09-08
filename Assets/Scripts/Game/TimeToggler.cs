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
        const float RepeatDelay = 0.4f;
        const float RepeatRate = 8f;

        private int _index = 1;
        float holdTime;
        int repeats;

        public static System.Collections.Generic.IReadOnlyList<uint> Ladder => Speeds;
        public uint Current => Speeds[_index];
        public TimeToggler Faster() => Shift(1);
        public TimeToggler Slower() => Shift(-1);

        public TimeToggler Shift(int steps)
        {
            _index = Mathf.Clamp(_index + steps, 0, Speeds.Length - 1);
            OnTimeChange();
            return this;
        }
        private void OnTimeChange()
        {
            Debug.Log($"Time speed changed to x{Current}");
        }
        void Update()
        {
            int direction = (Input.GetKey(KeyCode.Period) ? 1 : 0) - (Input.GetKey(KeyCode.Comma) ? 1 : 0);
            if (direction == 0)
            {
                holdTime = 0f;
                repeats = 0;
                return;
            }
            int steps = direction * (Input.GetKey(KeyCode.LeftShift) ? StepsPerDecade : 1);
            if (Input.GetKeyDown(KeyCode.Period) || Input.GetKeyDown(KeyCode.Comma))
            {
                Shift(steps);
                return;
            }
            holdTime += Time.deltaTime;
            if (holdTime < RepeatDelay || (holdTime - RepeatDelay) * RepeatRate < repeats) return;
            repeats++;
            Shift(steps);
        }
    }
}
