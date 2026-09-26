using DoublePrecision;
using OuterSpace.Sim;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

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
        // В файле — пути к ассетам станций в Resources; загрузчик подставляет их содержимое.
        [JsonProperty("stations")]
        public List<string> stationAssets = new();
        [JsonIgnore]
        public List<StationData> stations = new();
        public PlayerShip playerShip;
    }
    [Serializable]
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
        [NonSerialized]
        public KnowledgeSource knowledge;   // Не из файла: проставляется загрузчиком
    }
    [Serializable]
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
        public double methaneCapacity;      // кг
        public double loxCapacity;          // кг
        public Propulsion propulsion;
    }
    /// <summary>
    /// Станция не вращается: её оси совпадают с осями симуляции, поэтому узел задан прямо в
    /// них, смещением от центра станции.
    /// </summary>
    [Serializable]
    public class StationData : ObjectData
    {
        [NonSerialized, JsonIgnore]
        public List<PortData> ports;        // Не из данных: загрузчик снимает их с модели
        public BeaconData beacon;
        public CaptureData capture;
        public List<string> sells = new();   // "methane", "lox"
        [NonSerialized, JsonIgnore]
        public GameObject view;             // Не из данных: загрузчик берёт его из ассета
    }
    /// <summary>Допуски захвата: грубее — узел не защёлкивается.</summary>
    [Serializable]
    public class CaptureData
    {
        public double range;                // м, по оси узла
        public double lateral;              // м, вбок от оси
        public double speed;                // м/с, относительная скорость
        public double roll;                 // градусы
        public double pitch;                // градусы
        public double yaw;                  // градусы
    }
    /// <summary>Глиссадный маяк узла: ловится в конусе вокруг его оси.</summary>
    [Serializable]
    public class BeaconData
    {
        public double range;                // м
        public double cone;                 // полуугол, градусы
    }
    /// <summary>Стыковочный узел: у станции в осях симуляции, у корабля — в связанных.</summary>
    [Serializable]
    public class PortData
    {
        public Vector3d position;           // м
        public Vector3d axis;               // наружу из узла, единичный
        public Vector3d up;                 // задаёт крен, перпендикулярен оси
    }
}
