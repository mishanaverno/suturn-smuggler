using System;
using System.Collections.Generic;
using UnityEngine;

namespace OuterSpace
{
    public class SpaceMono : MonoBehaviour
    {
        public static SpaceMono instance;
        public List<PlanetMono> planets = new();

        private void Awake()
        {
            instance = this;
        }

    }
}
