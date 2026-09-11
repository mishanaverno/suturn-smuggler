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
        // Форма и цвет метки задаются тем, кто вешает компонент: корабль — синий треугольник,
        // тела — белые кольца. Треугольник это то же кольцо в три сегмента.
        public int ringSegments = 48;
        public Color ringColor = Color.white;

        Transform body;
        LineRenderer ring;

        void Start()
        {
            body = transform.Find("Body");
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
            line.startColor = line.endColor = ringColor;
            line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            line.sharedMaterial = SimLine.Material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }

        void LateUpdate()
        {
            NavDisplayMono display = NavDisplayMono.instance;
            if (display == null) return;

            double markerDiameter = display.MarkerSceneDiameter;
            double bodyDiameter = NavScale.BodySceneDiameter(
                GetComponent<SpaceObjectMono>().spaceObject.radius, SimView.metersPerSceneUnit);

            body.localScale = Vector3.one * (float)bodyDiameter;

            // Кольцо гаснет, когда диск его перерос: дальше размер показан по-настоящему.
            ring.enabled = NavScale.GlyphSceneDiameter(bodyDiameter, markerDiameter) == markerDiameter;
            float markerRadius = (float)markerDiameter * 0.5f;
            if (ring.enabled) DrawRing(display.cam, markerRadius, display.LineSceneWidth);
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
