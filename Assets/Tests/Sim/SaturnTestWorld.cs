using System;
using System.Collections.Generic;
using System.IO;
using DoublePrecision;
using Game;
using OuterSpace;
using OuterSpace.Sim;
using OuterSpace.Sim.Objects;
using UnityEngine;

/// <summary>
/// Настоящая система Сатурна из файла, собранная в SpaceObject'ы без сцены. Прогноз проверяется
/// на тех же расстояниях и сферах влияния, на которых он будет работать в игре: ширина окна
/// пролёта мимо Реи — минуты, и на выдуманных масштабах эта задача просто не воспроизводится.
/// </summary>
public class SaturnTestWorld : IDisposable
{
    public readonly SpaceObject saturn;
    public readonly List<SpaceObject> bodies = new();
    public readonly Dictionary<string, SpaceObject> byId = new();

    readonly List<SpaceObject> ordered = new();
    readonly List<GameObject> created = new();
    readonly GameObject prefab;
    readonly GameMono gameMono;

    public SaturnTestWorld()
    {
        GameObject host = new("GameMono");
        created.Add(host);
        gameMono = host.AddComponent<GameMono>();
        gameMono.enabled = false;
        // Awake в edit-mode не вызывается, поэтому ссылка проставляется вручную: двигатель
        // при включении сбрасывает перемотку через неё.
        gameMono.TimeToggler = host.AddComponent<TimeToggler>();
        gameMono.TimeToggler.enabled = false;
        GameMono.instance = gameMono;
        gameMono._epoch = 0;

        prefab = new GameObject("Prefab");
        created.Add(prefab);

        GameData data = SystemLoader.Load(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, GameMono.SystemFile)));
        foreach (ObjectData objData in data.system.objects)
        {
            SpaceObject obj = Create(objData.gm);
            obj.GameObject.name = objData.name;
            obj.radius = objData.radius;
            if (objData.orbit == null)
            {
                saturn = obj;
                obj.SetVelocity(Vector3d.zero);
            }
            else
            {
                obj.SetCentralBody(byId[objData.parent]);
                obj.SetOrbit(SimMono.ToElements(objData.orbit, obj.centralBody.MU));
                bodies.Add(obj);
            }
            byId.Add(objData.id, obj);
            ordered.Add(obj);
        }

        // Прогноз корабля и точка манёвра читают мир из статики SimMono: список тел,
        // цель и объект сцены, к которому подшивается метка манёвра.
        SimMono.bodies.Clear();
        SimMono.bodies.AddRange(bodies);
        SimMono.target = null;
        GameObject simHost = new("SimMono");
        created.Add(simHost);
        // Awake в edit-mode не вызывается, синглтон выставляется вручную — как у GameMono.
        SimMono.instance = simHost.AddComponent<SimMono>();
    }

    public double Epoch
    {
        get => gameMono._epoch;
        set => gameMono._epoch = value;
    }

    public SpaceObject this[string id] => byId[id];

    public SpaceObject Create(double mu)
    {
        SpaceObject obj = new(Vector3d.zero, Vector3d.zero, mu, prefab, new List<SpaceObject.SpaceObjectParts>());
        created.Add(obj.GameObject);
        return obj;
    }

    /// <summary>Объект с пренебрежимой массой на заданной орбите в заданный момент.</summary>
    public SpaceObject Put(SpaceObject central, OrbitElements orbit, double epoch)
    {
        Epoch = epoch;
        foreach (SpaceObject body in ordered) body.FixedUpdate();
        SpaceObject obj = Create(1.0);
        obj.SetCentralBody(central);
        obj.SetOrbit(orbit);
        return obj;
    }

    /// <summary>Корабль на заданной орбите: со своим прогнозом, манёвром и ориентацией.</summary>
    public Ship PutShip(SpaceObject central, OrbitElements orbit, double epoch)
    {
        Epoch = epoch;
        foreach (SpaceObject body in ordered) body.FixedUpdate();
        Ship ship = new(1000.0, prefab);
        created.Add(ship.GameObject);
        ship.SetCentralBody(central);
        ship.SetOrbit(orbit);
        return ship;
    }

    /// <summary>Тик симуляции с переподчинением по сферам влияния — как SimMono.FixedUpdate.</summary>
    public bool Step(double epoch, SpaceObject ship, SpaceObject other = null)
    {
        Epoch = epoch;
        foreach (SpaceObject body in ordered) body.FixedUpdate();
        other?.FixedUpdate();
        if (ship == null) return false;
        ship.FixedUpdate();

        SpaceObject central = SOITransition.ResolveCentralBody(ship, ship.centralBody, bodies, SOITransition.Hysteresis);
        if (central == ship.centralBody) return false;
        SOITransition.ChangeCentralBody(ship, central, epoch);
        return true;
    }

    public void Dispose()
    {
        foreach (GameObject go in created)
        {
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
        created.Clear();
        SimMono.bodies.Clear();
        SimMono.target = null;
        SimMono.instance = null;
        GameMono.instance = null;
    }
}
