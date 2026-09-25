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
        public List<StationData> stations = new();
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
        public double dryMass;              // кг
        public double methane;              // кг, начальный запас
        public double lox;                  // кг, начальный запас
        public Propulsion propulsion;
        // Стыковочные узлы по бортам, в связанных осях корабля (X вперёд, Y влево, Z вверх).
        public PortData leftPort;
        public PortData rightPort;
    }
    /// <summary>
    /// Станция не вращается: её оси совпадают с осями симуляции, поэтому узел задан прямо в
    /// них, смещением от центра станции.
    /// </summary>
    public class StationData : ObjectData
    {
        public PortData port;
        public List<string> sells = new();   // "methane", "lox"
    }
    /// <summary>Стыковочный узел: у станции в осях симуляции, у корабля — в связанных.</summary>
    public class PortData
    {
        public Vector3d position;           // м
        public Vector3d axis;               // наружу из узла, единичный
        public Vector3d up;                 // задаёт крен, перпендикулярен оси
    }
}
