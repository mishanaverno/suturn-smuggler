using System.Collections.Generic;
using OuterSpace.Sim.Objects;

namespace OuterSpace.Sim
{
    /// <summary>Груз по контракту: взят на одной станции, сдаётся на другой к сроку.</summary>
    public class Cargo
    {
        public string name;
        public double mass;                 // кг
        public Station from;
        public Station to;
        public double reward;               // cr
        public double deadline;             // эпоха, с
    }

    public class Hold
    {
        public double capacity;             // кг
        public readonly List<Cargo> cargo = new();

        public double Mass
        {
            get
            {
                double mass = 0.0;
                foreach (Cargo item in cargo) mass += item.mass;
                return mass;
            }
        }

        public double Free => capacity - Mass;
    }
}
