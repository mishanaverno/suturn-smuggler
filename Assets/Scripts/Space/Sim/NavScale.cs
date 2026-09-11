namespace OuterSpace.Sim
{
    /// <summary>
    /// Пересчёты масштаба навигационного экрана. Вынесены из MonoBehaviour, потому что
    /// потерянный множитель здесь не падает, а тихо делает картинку лживой, — а прибор
    /// обязан не врать ничем, по чему игрок принимает решение.
    /// </summary>
    public static class NavScale
    {
        // Видимая область прибора всегда занимает одно и то же число единиц сцены: за
        // дальностью следует не размер камеры, а масштаб сцены. Иначе на близких
        // дальностях не хватает float: Титан удалён от Сатурна на 1.2e9 м, и при
        // фиксированном масштабе его окрестности легли бы в шум мантиссы. По той же
        // причине начало сцены ставится в объект наблюдения - см. SimView.
        public const double OrthographicSize = 5.0;

        /// <summary>Дальность - половина высоты видимой области в метрах.</summary>
        public static double MetersPerSceneUnit(double rangeMeters) => rangeMeters / OrthographicSize;

        public static double SceneUnits(double meters, double metersPerSceneUnit) => meters / metersPerSceneUnit;

        public static double Meters(double sceneUnits, double metersPerSceneUnit) => sceneUnits * metersPerSceneUnit;

        public static double MetersPerPixel(double orthographicSize, double metersPerSceneUnit, int textureHeight)
            => 2.0 * orthographicSize * metersPerSceneUnit / textureHeight;

        /// <summary>
        /// Размер метки задан долей высоты экрана прибора, а не мировыми единицами и не
        /// пикселями окна: только так «метка в 12 пикселей» - определённая величина.
        /// </summary>
        public static double MarkerSceneDiameter(double markerFraction, double orthographicSize)
            => markerFraction * 2.0 * orthographicSize;

        public static double BodySceneDiameter(double radiusMeters, double metersPerSceneUnit)
            => 2.0 * radiusMeters / metersPerSceneUnit;

        /// <summary>
        /// Либо истинный размер, либо метка. Промежуточный размер читается как «вот такое
        /// тело» и потому врёт; истинный диск и круг фиксированного экранного размера
        /// с подписью не врут ни тот, ни другой.
        /// </summary>
        public static double GlyphSceneDiameter(double bodySceneDiameter, double markerSceneDiameter)
            => bodySceneDiameter >= markerSceneDiameter ? bodySceneDiameter : markerSceneDiameter;
    }
}
