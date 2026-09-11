using System;
using System.Collections.Generic;
using NUnit.Framework;
using OuterSpace.Sim;

/// <summary>
/// Проверки масштаба навигационного экрана. Отрисовку целиком тестами не покроешь,
/// поэтому покрыт весь счёт: потерянный множитель или перепутанные единицы здесь не
/// падают, а тихо делают картинку лживой, и глазами это не ловится.
/// </summary>
public class NavScaleTest
{
    const int Height = 768;
    const double MarkerFraction = 12.0 / 768.0;

    // Все ступени лестницы дальностей, а не пять избранных: проверять инварианты масштаба
    // дешевле на всех, чем гадать, какие из них крайние.
    static readonly double[] Ranges = BuildRanges();

    static double[] BuildRanges()
    {
        double[] ranges = new double[NavDisplayMono.RangeSteps];
        for (int i = 0; i < ranges.Length; i++) ranges[i] = NavDisplayMono.RangeAt(i);
        return ranges;
    }

    /// <summary>
    /// Лестница геометрическая: равные шаги ручки дают равные множители. Это и есть то
    /// свойство, ради которого она заменила пять фиксированных дальностей.
    /// </summary>
    [Test]
    public void RangeLadder_IsGeometric_AndSpansItsBounds()
    {
        Assert.AreEqual(NavDisplayMono.MinRange, Ranges[0], NavDisplayMono.MinRange * 1e-9);
        Assert.AreEqual(NavDisplayMono.MaxRange, Ranges[^1], NavDisplayMono.MaxRange * 1e-9);

        double factor = Ranges[1] / Ranges[0];
        for (int i = 1; i < Ranges.Length; i++)
        {
            Assert.Greater(Ranges[i], Ranges[i - 1]);
            Assert.AreEqual(factor, Ranges[i] / Ranges[i - 1], factor * 1e-9,
                $"шаг {i} меняет дальность не во столько же раз, что и остальные");
        }

        for (int i = 0; i < Ranges.Length; i++)
        {
            Assert.AreEqual(i, NavDisplayMono.StepAt(Ranges[i]), $"ступень {i} не находится по своей дальности");
        }
    }

    static double MetersPerPixel(double range, int textureHeight) =>
        NavScale.MetersPerPixel(NavScale.OrthographicSize, NavScale.MetersPerSceneUnit(range), textureHeight);

    [TestCase(1.3e10, 768)]
    [TestCase(1.3e10, 1080)]
    [TestCase(1.3e9, 768)]
    [TestCase(5.0e6, 768)]
    [TestCase(5.0e6, 256)]
    public void MetersPerPixel_IsViewHeightDividedByTextureHeight(double range, int textureHeight)
    {
        Assert.That(MetersPerPixel(range, textureHeight), Is.EqualTo(2.0 * range / textureHeight).Within(1e-9).Percent);
    }

    [Test]
    public void MarkerKeepsItsPixelSize_OnEveryRange()
    {
        foreach (double range in Ranges)
        {
            double metersPerSceneUnit = NavScale.MetersPerSceneUnit(range);
            double markerMeters = NavScale.Meters(
                NavScale.MarkerSceneDiameter(MarkerFraction, NavScale.OrthographicSize), metersPerSceneUnit);
            double pixels = markerMeters / MetersPerPixel(range, Height);
            Assert.That(pixels, Is.EqualTo(12.0).Within(1e-9), $"дальность {range:E1}");
        }
    }

    /// <summary>
    /// Метка задана долей высоты экрана прибора, и её размер зависит только от разрешения
    /// текстуры прибора. Разрешение окна в счёт не входит вовсе — именно ради этого
    /// прибор рисуется в свою текстуру фиксированного разрешения.
    /// </summary>
    [TestCase(768, 12.0)]
    [TestCase(1536, 24.0)]
    [TestCase(384, 6.0)]
    public void MarkerPixelSize_FollowsTextureHeightOnly(int textureHeight, double expectedPixels)
    {
        foreach (double range in Ranges)
        {
            double metersPerSceneUnit = NavScale.MetersPerSceneUnit(range);
            double markerMeters = NavScale.Meters(
                NavScale.MarkerSceneDiameter(MarkerFraction, NavScale.OrthographicSize), metersPerSceneUnit);
            Assert.That(markerMeters / MetersPerPixel(range, textureHeight), Is.EqualTo(expectedPixels).Within(1e-9));
        }
    }

    [Test]
    public void RangeAndSceneUnits_AreMutuallyInverse()
    {
        foreach (double range in Ranges)
        {
            double metersPerSceneUnit = NavScale.MetersPerSceneUnit(range);
            Assert.That(NavScale.SceneUnits(range, metersPerSceneUnit),
                Is.EqualTo(NavScale.OrthographicSize).Within(1e-9).Percent, $"дальность {range:E1}");
            Assert.That(NavScale.Meters(NavScale.SceneUnits(range, metersPerSceneUnit), metersPerSceneUnit),
                Is.EqualTo(range).Within(1e-9).Percent);
        }
    }

    // Средние радиусы, м: NASA JPL SSD. Корабль — из файла системы.
    static readonly Dictionary<string, double> Radii = new()
    {
        { "saturn", 5.8232e7 },
        { "titan", 2.5747e6 },
        { "mimas", 1.9820e5 },
        { "ship", 50.0 },
    };

    // Диаметр тела в пикселях на экране высотой 768. Столбец «м/пиксель» и сами значения
    // в таблице округлены до двух-трёх значащих цифр, поэтому допуск — 2 % или 0.005
    // пикселя, что больше. Этого хватает, чтобы поймать и ошибку в единицах, и потерянный
    // множитель: они дают промах в разы, а не в проценты.
    [TestCase(3.4e7, 3.4, 0.15, 0.01, 0.0)]
    [TestCase(9.6e6, 12.1, 0.53, 0.04, 0.0)]
    [TestCase(3.4e6, 34.4, 1.5, 0.12, 0.0)]
    [TestCase(1.3e5, 894.0, 39.6, 3.0, 0.0)]
    [TestCase(1.3e4, 8944.0, 395.0, 30.4, 0.01)]
    public void BodyDiskInPixels_MatchesReferenceTable(
        double metersPerPixel, double saturn, double titan, double mimas, double ship)
    {
        // Обратный ход таблицы: из «метров на пиксель» — дальность и масштаб сцены.
        double range = metersPerPixel * Height / 2.0;
        double metersPerSceneUnit = NavScale.MetersPerSceneUnit(range);

        Dictionary<string, double> expected = new()
        {
            { "saturn", saturn }, { "titan", titan }, { "mimas", mimas }, { "ship", ship },
        };
        foreach (KeyValuePair<string, double> entry in expected)
        {
            double sceneDiameter = NavScale.BodySceneDiameter(Radii[entry.Key], metersPerSceneUnit);
            double pixels = NavScale.Meters(sceneDiameter, metersPerSceneUnit) / MetersPerPixel(range, Height);
            double tolerance = Math.Max(0.02 * entry.Value, 0.005);
            Assert.That(pixels, Is.EqualTo(entry.Value).Within(tolerance),
                $"{entry.Key} при {metersPerPixel:E1} м/пиксель");
        }
    }

    /// <summary>
    /// Запрещённое состояние: тело, нарисованное размером, который не является ни истинным,
    /// ни явно символическим. Промежуточный размер читается как «вот такое тело» и врёт.
    /// </summary>
    [Test]
    public void Glyph_IsEitherTrueDiskOrMarker_NeverBetween()
    {
        foreach (double range in Ranges)
        {
            double metersPerSceneUnit = NavScale.MetersPerSceneUnit(range);
            double marker = NavScale.MarkerSceneDiameter(MarkerFraction, NavScale.OrthographicSize);
            foreach (double radius in Radii.Values)
            {
                double body = NavScale.BodySceneDiameter(radius, metersPerSceneUnit);
                double glyph = NavScale.GlyphSceneDiameter(body, marker);
                Assert.IsTrue(glyph == body || glyph == marker,
                    $"радиус {radius:E1} на дальности {range:E1}: размер {glyph} не равен ни истинному {body}, ни метке {marker}");
                Assert.That(glyph, Is.GreaterThanOrEqualTo(marker));
            }
        }
    }
}
