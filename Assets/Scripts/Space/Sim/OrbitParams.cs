using DoublePrecision;

namespace OuterSpace
{
    public class OrbitElements
    {
        public double semiMajorAxis;        // Большая полуось (a), м
        public double eccentricity;         // Эксцентриситет (e)
        public double inclination;          // Наклон орбиты (i), градусы
        public double longitudeOfAscendingNode; // Долгота восходящего узла (Ω), градусы
        public double argumentOfPeriapsis;  // Аргумент перицентра (ω), градусы
        public double meanAnomalyAtEpoch; // средняя аномалия в стартовую эпоху (М), радианы
        public double startEpoch;            // Время эпохи (в секундах от некоторой начальной точки)
        public double mu;
    }
}
