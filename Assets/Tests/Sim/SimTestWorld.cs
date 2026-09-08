using System;
using System.Collections.Generic;
using DoublePrecision;
using Game;
using OuterSpace;
using UnityEngine;

/// <summary>
/// Минимальная система «звезда — планета — луна» для edit-mode тестов.
/// SpaceObject требует GameObject и GameMono.instance, но не требует сцены,
/// поэтому и то и другое создаётся здесь вручную.
/// </summary>
public class SimTestWorld : IDisposable
{
    public const double StarMU = 1.989e30 * Constants.realG;
    public const double PlanetMU = 5.97e24 * Constants.realG;
    public const double MoonMU = 7.35e22 * Constants.realG;
    public const double PlanetOrbit = 1.5e11;
    public const double MoonOrbit = 3.844e8;

    public readonly SpaceObject star;
    public readonly SpaceObject planet;
    public readonly SpaceObject moon;
    public readonly List<SpaceObject> bodies = new();

    readonly List<GameObject> created = new();
    readonly GameObject prefab;
    readonly GameMono gameMono;

    public SimTestWorld()
    {
        GameObject host = new GameObject("GameMono");
        created.Add(host);
        gameMono = host.AddComponent<GameMono>();
        // Иначе GameMono.Update в play-mode крутит эпоху сам и падает на пустом TimeToggler.
        gameMono.enabled = false;
        GameMono.instance = gameMono;
        gameMono._epoch = 0;

        prefab = new GameObject("Prefab");
        created.Add(prefab);

        star = Create(Vector3d.zero, StarMU);
        star.GameObject.name = "Star";
        star.SetVelocity(Vector3d.zero);

        planet = Create(new Vector3d(PlanetOrbit, 0, 0), PlanetMU);
        planet.GameObject.name = "Planet";
        planet.SetCentralBody(star);
        planet.SetVelocity(new Vector3d(0, CircularSpeed(star.MU, PlanetOrbit), 0));

        moon = Create(new Vector3d(PlanetOrbit + MoonOrbit, 0, 0), MoonMU);
        moon.GameObject.name = "Moon";
        moon.SetCentralBody(planet);
        moon.SetVelocity(new Vector3d(0, CircularSpeed(planet.MU, MoonOrbit), 0));

        bodies.Add(planet);
        bodies.Add(moon);
    }

    public double Epoch
    {
        get => gameMono._epoch;
        set => gameMono._epoch = value;
    }

    public static double CircularSpeed(double mu, double radius) => Math.Sqrt(mu / radius);

    public SpaceObject Create(Vector3d globalPosition, double mu)
    {
        SpaceObject obj = new(globalPosition, Vector3d.zero, mu, prefab, new List<SpaceObject.SpaceObjectParts>());
        created.Add(obj.GameObject);
        return obj;
    }

    /// <summary>Объект на заданной глобальной позиции с заданной скоростью относительно центрального тела.</summary>
    public SpaceObject CreateOrbiting(SpaceObject central, Vector3d relativePosition, Vector3d relativeVelocity, double mu = 1000.0 * Constants.realG)
    {
        SpaceObject obj = Create(central.simTransform.GLOBAL_R + relativePosition, mu);
        obj.SetCentralBody(central);
        obj.SetVelocity(relativeVelocity);
        return obj;
    }

    /// <summary>Тик симуляции: родители обновляются раньше детей.</summary>
    public void Step(double epoch, params SpaceObject[] children)
    {
        gameMono._epoch = epoch;
        planet.FixedUpdate();
        moon.FixedUpdate();
        foreach (SpaceObject child in children) child.FixedUpdate();
    }

    public void Dispose()
    {
        foreach (GameObject go in created)
        {
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
        created.Clear();
        GameMono.instance = null;
    }
}
