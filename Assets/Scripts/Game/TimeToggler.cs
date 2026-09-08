using UnityEngine;

namespace Game
{
    public class TimeToggler : MonoBehaviour
    {
        // Ровная лестница: каждая ступень — сотня или десятка к предыдущей, и игрок знает,
        // во сколько раз ускорился, не глядя на число. 1e5 — это виток корабля вокруг Титана
        // за три секунды и оборот Титана за 14 секунд.
        static readonly uint[] Speeds = { 0, 1, 100, 1000, 10000, 100000 };
        private int _index = 1;

        public uint Current => Speeds[_index];
        public TimeToggler Faster()
        {
            if (_index + 1 < Speeds.Length)
            {
                _index++;
            }
            OnTimeChange();
            return this;
        }
        public TimeToggler Slower()
        {
            if (_index > 0)
            {
                _index--;
            }
            OnTimeChange();
            return this;
        }
        private void OnTimeChange()
        {
            Debug.Log($"Time speed changed to x{Current}");
        }
        void Update()
        {
            if (Input.GetKeyUp(KeyCode.Period))
            {
                Faster();
            }
            if (Input.GetKeyUp(KeyCode.Comma))
            {
                Slower();
            }
        }
    }
}