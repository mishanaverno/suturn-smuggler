using System.Collections;
using System;

namespace OuterSpace
{
    public static class Constants
    {
        public static double realG = 6.674e-11;
        public static double simDistanceMultiplier = 1e+9; // Множитель расстояний в симуляции
        public static double simMassMultiplier = 1e+20; // Множитель массы в симуляции
        public static double Tolerance = 1e-11; // Допуск для сравнения с нулем
        public static double G = Constants.realG * (Constants.simMassMultiplier / Math.Pow(Constants.simDistanceMultiplier, 3));
    }
}
