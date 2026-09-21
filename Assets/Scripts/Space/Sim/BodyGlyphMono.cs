using UnityEngine;

namespace OuterSpace.Sim
{
    /// <summary>
    /// Тело на экране прибора рисуется двумя элементами сразу: диском истинного масштаба
    /// и кольцом-меткой фиксированного экранного размера с подписью. Пока тело меньше
    /// кольца, видно только кольцо, и оно честно означает «здесь объект, размер не показан»;
    /// при приближении диск вырастает изнутри кольца и перерастает его. Размера,
    /// который не является ни истинным, ни явно символическим, не бывает ни на одной
    /// дальности - именно он и заставил бы поднимать орбиту корабля.
    ///
    /// Имени тело больше не носит: подписи выстраивает NavLabelRail по краям экрана. Пока
    /// каждое тело подписывало себя само, уступить место было некому — знать про соседей
    /// может только тот, кто видит всех.
    /// </summary>
    public class BodyGlyphMono : MonoBehaviour
    {
        // Форма метки задаётся тем, кто вешает компонент: у корабля треугольник, у тела
        // кольцо. Треугольник это то же кольцо в три сегмента. Цвет компонент не принимает
        // ни от кого: он не свойство тела, а его сегодняшняя роль — см. NavPalette.
        public int ringSegments = 48;

        Transform body;
        Renderer bodyRenderer;
        LineRenderer ring;
        SpaceObjectMono owner;
        MaterialPropertyBlock tint;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Start()
        {
            body = transform.Find("Body");
            bodyRenderer = body == null ? null : body.GetComponent<Renderer>();
            owner = GetComponent<SpaceObjectMono>();
            tint = new MaterialPropertyBlock();
            ring = CreateRing();
        }

        LineRenderer CreateRing()
        {
            GameObject ringObject = new("Marker");
            ringObject.layer = gameObject.layer;
            ringObject.transform.SetParent(transform, false);
            LineRenderer line = ringObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = ringSegments;
            line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            line.sharedMaterial = SimLine.Material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }

        void LateUpdate()
        {
            NavDisplayPanel display = NavDisplayPanel.instance;
            if (display == null) return;

            SpaceObject obj = owner.spaceObject;
            double markerDiameter = display.MarkerSceneDiameter;
            double bodyDiameter = NavScale.BodySceneDiameter(obj.radius, SimView.metersPerSceneUnit);

            body.localScale = Vector3.one * (float)bodyDiameter;
            Color role = NavPalette.For(obj);
            ring.startColor = ring.endColor = role;
            Paint(obj, role, display);

            // Кольцо гаснет, когда диск его перерос: дальше размер показан по-настоящему.
            ring.enabled = NavScale.GlyphSceneDiameter(bodyDiameter, markerDiameter) == markerDiameter;
            float markerRadius = (float)markerDiameter * 0.5f;
            if (ring.enabled) DrawRing(display.cam, markerRadius, display.LineSceneWidth);
        }

        /// <summary>
        /// Залит только тот, на кого сейчас смотрят. Заливка — самое громкое, что есть на
        /// приборе, и раздавать её всем телам подряд значит не говорить ничего: диск Сатурна
        /// тянул внимание сильнее любого события, хотя событием не был.
        /// </summary>
        void Paint(SpaceObject obj, Color role, NavDisplayPanel display)
        {
            if (bodyRenderer == null) return;
            bool focused = display.focus != null && ReferenceEquals(display.focus, obj.simTransform);
            tint.SetColor(ColorId, focused ? NavPalette.Dim(role, 0.7f) : NavPalette.Neutral);
            bodyRenderer.SetPropertyBlock(tint);
        }

        // Метка развёрнута к камере: круг в фиксированной плоскости при повороте вида
        // превращался бы в эллипс и означал бы не то, чем является.
        void DrawRing(Camera cam, float radius, float width)
        {
            ring.widthMultiplier = width;
            Vector3 right = transform.InverseTransformDirection(cam.transform.right) * radius;
            Vector3 up = transform.InverseTransformDirection(cam.transform.up) * radius;
            for (int i = 0; i < ringSegments; i++)
            {
                // Отсчёт от вертикали: у треугольника это вершина вверх, у кольца незаметно.
                float angle = Mathf.PI / 2f + 2f * Mathf.PI * i / ringSegments;
                ring.SetPosition(i, right * Mathf.Cos(angle) + up * Mathf.Sin(angle));
            }
        }
    }
}
