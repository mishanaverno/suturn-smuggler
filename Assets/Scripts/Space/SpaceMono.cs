using UnityEngine;

namespace OuterSpace
{
    public class SpaceMono : MonoBehaviour
    {
        public static SpaceMono instance;

        private void Awake()
        {
            instance = this;
        }
    }
}
