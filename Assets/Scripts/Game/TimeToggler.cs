using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game
{
    public class TimeToggler : MonoBehaviour
    {
        public enum TimeSpeed
        {
            pause,
            normal,
            fast,
            extrafast
        }
        private KeyValuePair<TimeSpeed, uint>[] _dict =  new Dictionary<TimeSpeed, uint>() {
            { TimeSpeed.pause, 0 },
            { TimeSpeed.normal, 1 },
            { TimeSpeed.fast, 6000 },
            { TimeSpeed.extrafast, 66000 } 
        }.ToArray();
        private uint _index = 0;

        public KeyValuePair<TimeSpeed, uint> Current => _dict[_index];
        public TimeToggler Faster()
        {
            if (_index + 1 < _dict.Length)
            {
                _index++;
            }
            OnTimeChange();
            return this;
        }
        public TimeToggler Slower()
        {
            if ( _index > 0)
            {
                _index--;
            }
            OnTimeChange();
            return this;
        }
        private void OnTimeChange()
        {
            Debug.Log($"Time speed changed to {Current.Key} = {Current.Value}");
        }
        void Update()
        {
            if (Input.GetKeyUp(KeyCode.KeypadPlus))
            {
                Faster();
            }
            if (Input.GetKeyUp(KeyCode.KeypadMinus))
            {
                Slower();
            }
        }
    }
}