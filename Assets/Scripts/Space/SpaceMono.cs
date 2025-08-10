using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OuterSpace
{
    public class SpaceMono : MonoBehaviour
    {
        public static SpaceMono instance;
        public List<PlanetMono> planets = new();
        public double G = 1;
        private void Awake()
        {
            instance = this;
        }
    }
}
