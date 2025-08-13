using System.Collections;
using System;

namespace OuterSpace
{
    public static class Constanst
    {
        public static double realG = 6.674e-11;
        public static double simDistanceMultiplier = 1e+9; // ћножитель рассто€ний в симул€ции
        public static double simMassMultiplier = 1e+20; // ћножитель массы в симул€ции
        public static double Tolerance = 0.000000000000000000000000000000001; // ƒопуск дл€ сравнени€ с нулем
        public static double G = Constanst.realG * (Constanst.simMassMultiplier / Math.Pow(Constanst.simDistanceMultiplier, 3));
    }
}
