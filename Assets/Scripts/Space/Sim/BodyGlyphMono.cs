using TMPro;
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
    /// </summary>
    public class BodyGlyphMono : MonoBehaviour
    {
        const string DefaultFontPath = "Fonts & Materials/LiberationSans SDF";

        // Форма и цвет метки задаются тем, кто вешает компонент: корабль — синий треугольник,
        // тела — белые кольца. Треугольник это то же кольцо в три сегмента.
        public int ringSegments = 48;
        public Color ringColor = Color.white;

        Transform body;
        LineRenderer ring;
        TextMeshPro label;
        float labelLineHeight;

        void Start()
        {
            body = transform.Find("Body");
            ring = CreateRing();
            label = CreateLabel();
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

        TextMeshPro CreateLabel()
        {
            GameObject labelObject = new("Label");
            labelObject.layer = gameObject.layer;
            labelObject.transform.SetParent(transform, false);
            TextMeshPro text = labelObject.AddComponent<TextMeshPro>();
            // Умолчание TMP берёт из TMP Settings по guid, а тот в этом проекте уезжал при
            // переимпорте TMP Essentials: текст оставался без шрифта и не рисовался, молча.
            // Загрузка по пути от guid не зависит.
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null) font = Resources.Load<TMP_FontAsset>(DefaultFontPath);
            text.font = font;
            text.text = transform.name;
            text.fontSize = 1f;
            text.alignment = TextAlignmentOptions.Left;
            text.rectTransform.sizeDelta = new Vector2(20f, 2f);
            text.rectTransform.pivot = new Vector2(0f, 0.5f);
            // fontSize у TextMeshPro - не высота строки в мировых единицах: для не-
            // ортографического текста TMP домножает её ещё и на 0.1. Чтобы подпись
            // занимала ровно заданную долю высоты экрана, высота строки измеряется,
            // а не выводится из константы.
            text.ForceMeshUpdate();
            labelLineHeight = text.preferredHeight;
            return text;
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

            label.transform.rotation = display.cam.transform.rotation;
            label.transform.localPosition = display.cam.transform.rotation * new Vector3(markerRadius, markerRadius, 0f);
            label.transform.localScale = Vector3.one * (float)display.LabelSceneHeight / labelLineHeight;
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
