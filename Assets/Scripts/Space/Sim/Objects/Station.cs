using System.Collections.Generic;
using DoublePrecision;
using Game;
using UnityEngine;

namespace OuterSpace.Sim.Objects
{
    /// <summary>
    /// Орбитальная станция. Не тяготеет и не вращается: оси станции — оси симуляции, поэтому
    /// узел неподвижен относительно её центра, и совмещаться корабль будет с постоянным
    /// направлением.
    /// </summary>
    public class Station : SpaceObject
    {
        public readonly PortData port;
        public readonly IReadOnlyList<string> sells;

        public Station(PortData port, IReadOnlyList<string> sells, GameObject prefab)
            : base(Vector3d.zero, Vector3d.zero, 0.0, prefab, new() { SpaceObjectParts.ORBIT })
        {
            this.port = port;
            this.sells = sells;
        }
    }
}
