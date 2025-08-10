using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OuterSpace
{
    public sealed class OrbitParams
    {
        public readonly double mu; // гравитационный параметр (mu)
        public readonly double semiMajorAxis; // Большая полуось (a)
        public readonly double eccentricity; // Эксцентриситет (e)
        public readonly double inclination; // Наклонение (i)
        public readonly double longitudeOfAscendingNode; // Долгота восходящего узла (Omega)
        public readonly double argumentOfPericenter; // Аргумент перицентра (omega)
        public readonly double trueAnomaly; // Истинная аномалия (nu)

        public OrbitParams(double mu, double semiMajorAxis, double eccentricity, double inclination, double longitudeOfAscendingNode, double argumentOfPericenter, double trueAnomaly)
        {
            this.mu = mu;
            this.semiMajorAxis = semiMajorAxis;
            this.eccentricity = eccentricity;
            this.inclination = inclination;
            this.longitudeOfAscendingNode = longitudeOfAscendingNode;
            this.argumentOfPericenter = argumentOfPericenter;
            this.trueAnomaly = trueAnomaly;
        }
    }
}
