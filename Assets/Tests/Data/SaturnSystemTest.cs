using System;
using System.Collections.Generic;
using System.IO;
using DoublePrecision;
using Game;
using Newtonsoft.Json;
using NUnit.Framework;
using OuterSpace;
using OuterSpace.Sim;
using UnityEngine;

/// <summary>
/// Проверки файла системы Сатурна. Ошибка в одной цифре здесь не падает, а тихо делает
/// мир неправильным, поэтому справочные значения периодов заданы независимо от файла.
/// </summary>
public class SaturnSystemTest
{
    // Периоды обращения в сутках, NASA JPL SSD. Сравниваются с посчитанными из a и GM Сатурна.
    static readonly Dictionary<string, double> ReferencePeriods = new()
    {
        { "mimas", 0.942 },
        { "enceladus", 1.370 },
        { "tethys", 1.888 },
        { "dione", 2.737 },
        { "rhea", 4.518 },
        { "titan", 15.945 },
        { "hyperion", 21.28 },
        { "iapetus", 79.33 },
        { "phoebe", 550.3 },
    };

    GameData data;
    ObjectData root;

    [SetUp]
    public void SetUp()
    {
        data = SystemLoader.Load(Json());
        root = data.system.objects[0];
    }

    static string Json() => File.ReadAllText(Path.Combine(Application.streamingAssetsPath, GameMono.SystemFile));

    static double SOI(ObjectData body, ObjectData central) =>
        body.orbit.semiMajorAxis * Math.Pow(body.gm / central.gm, 2.0 / 5.0);

    double PeriodDays(ObjectData body) =>
        2.0 * Math.PI * Math.Sqrt(Math.Pow(body.orbit.semiMajorAxis, 3) / root.gm) / 86400.0;

    [Test]
    public void AllBodiesLoaded_WithUniqueIds()
    {
        Assert.AreEqual(ReferencePeriods.Count + 1, data.system.objects.Count);

        HashSet<string> ids = new();
        foreach (ObjectData obj in data.system.objects)
        {
            Assert.IsFalse(string.IsNullOrEmpty(obj.id), "пустой id");
            Assert.IsTrue(ids.Add(obj.id), $"id повторяется: {obj.id}");
        }
        foreach (string id in ReferencePeriods.Keys) Assert.Contains(id, new List<string>(ids));
    }

    [Test]
    public void Hierarchy_HasSingleRoot_AndParentsComeFirst()
    {
        HashSet<string> seen = new();
        int roots = 0;
        foreach (ObjectData obj in data.system.objects)
        {
            if (string.IsNullOrEmpty(obj.parent)) roots++;
            else Assert.IsTrue(seen.Contains(obj.parent), $"{obj.id}: родитель {obj.parent} не создан раньше");
            seen.Add(obj.id);
        }
        Assert.AreEqual(1, roots);
        Assert.AreSame(root, data.system.objects[0]);
    }

    [Test]
    public void Periods_MatchReferenceWithinOnePercent()
    {
        foreach (ObjectData obj in data.system.objects)
        {
            if (obj == root) continue;
            double expected = ReferencePeriods[obj.id];
            double actual = PeriodDays(obj);
            Assert.That(Math.Abs(actual - expected) / expected, Is.LessThan(0.01),
                $"{obj.id}: период {actual:F3} сут против справочных {expected:F3}");
        }
    }

    [Test]
    public void SphereOfInfluence_IsBetweenBodyRadiusAndOrbit()
    {
        foreach (ObjectData obj in data.system.objects)
        {
            if (obj == root) continue;
            double soi = SOI(obj, root);
            Assert.Greater(soi, obj.radius, $"{obj.id}: SOI меньше радиуса тела");
            Assert.Less(soi, obj.orbit.semiMajorAxis, $"{obj.id}: SOI больше большой полуоси");
        }
        ObjectData titan = data.system.objects.Find(o => o.id == "titan");
        Assert.That(SOI(titan, root), Is.EqualTo(4.33e7).Within(1).Percent);
    }

    [Test]
    public void SpheresOfInfluence_DoNotOverlap()
    {
        List<ObjectData> byOrbit = data.system.objects.FindAll(o => o != root);
        byOrbit.Sort((a, b) => a.orbit.semiMajorAxis.CompareTo(b.orbit.semiMajorAxis));

        for (int i = 1; i < byOrbit.Count; i++)
        {
            ObjectData inner = byOrbit[i - 1];
            ObjectData outer = byOrbit[i];
            double gap = outer.orbit.semiMajorAxis - inner.orbit.semiMajorAxis;
            double sum = SOI(inner, root) + SOI(outer, root);
            Assert.Less(sum, gap, $"сферы влияния {inner.id} и {outer.id} пересекаются");
        }
    }

    [Test]
    public void PlayerShip_OrbitsInsideParentSOI_AboveSurface()
    {
        PlayerShip ship = data.system.playerShip;
        ObjectData parent = data.system.objects.Find(o => o.id == ship.parent);
        Assert.NotNull(parent, $"родитель корабля {ship.parent} не найден");

        double periapsis = ship.orbit.semiMajorAxis * (1.0 - ship.orbit.eccentricity);
        double apoapsis = ship.orbit.semiMajorAxis * (1.0 + ship.orbit.eccentricity);
        Assert.Greater(periapsis, parent.radius, "корабль под поверхностью");
        Assert.Less(apoapsis, SOI(parent, root), "корабль вне сферы влияния родителя");
    }

    [Test]
    public void EveryObject_KnownFromDatabase_AfterLoad()
    {
        List<ObjectData> all = new(data.system.objects) { data.system.playerShip };
        foreach (ObjectData obj in all)
            Assert.AreEqual(KnowledgeSource.Database, obj.knowledge, $"{obj.id}: источник знания");
    }

    [Test]
    public void EccentricityAndInclination_AreInRange()
    {
        List<ObjectData> all = new(data.system.objects) { data.system.playerShip };
        foreach (ObjectData obj in all)
        {
            if (obj.orbit == null) continue;
            Assert.That(obj.orbit.eccentricity, Is.InRange(0.0, 0.999999), $"{obj.id}: эксцентриситет");
            Assert.That(obj.orbit.inclination, Is.InRange(0.0, 180.0), $"{obj.id}: наклонение");
        }
    }

    /// <summary>Путь, которым SimMono ставит тело на орбиту: элементы из файла — вектор состояния.</summary>
    [Test]
    public void ElementsFromFile_GiveOrbitThatClosesAfterOnePeriod()
    {
        foreach (ObjectData obj in data.system.objects)
        {
            if (obj == root) continue;
            OrbitElements elements = SimMono.ToElements(obj.orbit, root.gm);
            (Vector3d start, _) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(elements, elements.startEpoch);
            (Vector3d end, _) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(elements, elements.startEpoch + PeriodDays(obj) * 86400.0);

            (double periapsis, double apoapsis) = AstroDynamic.GetPeriapsisAndApoapsis(elements);
            Assert.That(start.magnitude, Is.InRange(periapsis, apoapsis), $"{obj.id}: радиус вне [перицентр, апоцентр]");
            Assert.Less((end - start).magnitude, 1e-6 * start.magnitude, $"{obj.id}: орбита не замкнулась за период");
        }
    }

    [Test]
    public void Load_IsIdempotent()
    {
        // Сериализуется только состав системы: Vector3d.normalized рекурсивен и Json.NET на нём зацикливается.
        Assert.AreEqual(Dump(SystemLoader.Load(Json())), Dump(SystemLoader.Load(Json())));
    }

    static string Dump(GameData loaded) =>
        JsonConvert.SerializeObject(new object[] { loaded.startEpoch, loaded.system.objects, loaded.system.playerShip });

    // Синтетическая система: битые данные проверяются на ней, а не подменой строк
    // в настоящем файле — так видно, что именно сломано.
    static string Synthetic(string bodies) =>
        ("{'startEpoch':0,'system':{'objects':[" + bodies + "],'playerShip':" + ShipBody + "}}").Replace('\'', '"');

    const string RootBody = "{'id':'root','name':'Root','gm':3.7931206e16,'radius':5.8232e7}";
    const string MoonBody = "{'id':'moon','name':'Moon','parent':'root','gm':8.9781371e12,'radius':2.5747e6,'orbit':{'semiMajorAxis':1.2219e9,'eccentricity':0.029,'inclination':0.3,'epoch':0}}";
    const string ShipBody = "{'id':'player','name':'player','parent':'moon','mass':100000,'radius':50,'orbit':{'semiMajorAxis':2.9e6,'eccentricity':0,'inclination':0,'epoch':0}}";

    [TestCase("родитель не существует", RootBody + "," + MoonBody, "'parent':'root'", "'parent':'ganymede'", "не найдено")]
    [TestCase("отрицательный GM", RootBody + "," + MoonBody, "'gm':8.9781371e12", "'gm':-8.9781371e12", "GM")]
    [TestCase("гипербола у луны", RootBody + "," + MoonBody, "'eccentricity':0.029", "'eccentricity':1.5", "эксцентриситет")]
    [TestCase("луна без орбиты", RootBody + "," + MoonBody, ",'orbit':{'semiMajorAxis':1.2219e9,'eccentricity':0.029,'inclination':0.3,'epoch':0}}", "}", "орбита")]
    public void BrokenData_ThrowsReadableError(string _, string bodies, string original, string replacement, string expected)
    {
        string broken = Synthetic(bodies.Replace(original, replacement));
        Assert.AreNotEqual(Synthetic(bodies), broken, "подмена не сработала, тест проверяет не то, что задумано");

        SystemDataException error = Assert.Throws<SystemDataException>(() => SystemLoader.Load(broken));
        StringAssert.Contains(expected, error.Message);
    }

    [Test]
    public void TwoRoots_ThrowReadableError()
    {
        const string second = "{'id':'root2','name':'Root2','gm':1e16,'radius':1e7}";

        SystemDataException error = Assert.Throws<SystemDataException>(
            () => SystemLoader.Load(Synthetic(RootBody + "," + MoonBody + "," + second)));
        StringAssert.Contains("ровно один корень", error.Message);
    }

    [Test]
    public void CycleInHierarchy_ThrowsReadableError()
    {
        const string a = "{'id':'a','name':'A','parent':'b','gm':1e10,'radius':1e5,'orbit':{'semiMajorAxis':1e8,'eccentricity':0,'inclination':0,'epoch':0}}";
        const string b = "{'id':'b','name':'B','parent':'a','gm':1e10,'radius':1e5,'orbit':{'semiMajorAxis':2e8,'eccentricity':0,'inclination':0,'epoch':0}}";

        SystemDataException error = Assert.Throws<SystemDataException>(
            () => SystemLoader.Load(Synthetic(RootBody + "," + MoonBody + "," + a + "," + b)));
        StringAssert.Contains("Цикл", error.Message);
    }

    [Test]
    public void ValidSyntheticSystem_Loads()
    {
        Assert.AreEqual(2, SystemLoader.Load(Synthetic(RootBody + "," + MoonBody)).system.objects.Count);
    }
}
