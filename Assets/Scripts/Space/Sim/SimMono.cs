using UnityEngine;

namespace OuterSpace.Sim
{
    public class SimMono : MonoBehaviour
    {
        public static SimMono instance;

        private void Awake()
        {
            instance = this;
        }
    }
}