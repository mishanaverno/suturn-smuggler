using DoublePrecision;
using OuterSpace.Sim;
using System.Collections.Generic;

namespace Game
{
    public class GameData
    {
        public double startEpoch;
        public SystemData system;
    }
    public class SystemData
    {
        // Солнце не моделируется как тело: вся игра идёт внутри системы Сатурна.
        // Направление и расстояние понадобятся позже для освещения и тепловой механики.
        public Vector3d sunDirection;
        public double sunDistance;
        // После загрузки — обходом иерархии от корня: родитель всегда раньше ребёнка.
        public List<ObjectData> objects;
        public PlayerShip playerShip;
    }
    public class ObjectData
    {
        public string id;
        public string name;
        public string description;
        public string parent;
        public double gm;                   // Гравитационный параметр GM, м³/с²
        public double radius;               // Средний радиус тела, м
        public string simPrefab;
        public OrbitData orbit;             // У корня системы отсутствует
        public KnowledgeSource knowledge;   // Не из файла: проставляется загрузчиком
    }
    public class OrbitData
    {
        public double semiMajorAxis;            // a, м
        public double eccentricity;             // e
        public double inclination;              // i, градусы
        public double longitudeOfAscendingNode; // Ω, градусы
        public double argumentOfPeriapsis;      // ω, градусы
        public double meanAnomalyAtEpoch;       // M: в файле градусы, после загрузки радианы
        public double epoch;                    // Секунды от начала отсчёта игры
    }
    public class PlayerShip : ObjectData
    {
        public double mass;                 // кг: у корабля масса нужна по-настоящему, для тяги
    }
}
