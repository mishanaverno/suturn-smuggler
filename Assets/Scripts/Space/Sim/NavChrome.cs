using System;
using TMPro;
using UnityEngine;

namespace OuterSpace.Sim
{
    /// <summary>
    /// Разметка стекла: масштабная линейка и опорное направление.
    ///
    /// Пустое поле прибора читается как «забыли отрисовать», а по картинке без меры нельзя
    /// сказать ни насколько далеко то, что видно, ни куда оно повёрнуто: и то и другое
    /// известно прибору, но не показано. Поэтому разметка — не украшение углов, а те два
    /// числа, без которых остальное изображение не переводится в решения.
    ///
    /// Стоит она в долях кадра камеры, а не в мире: линейка меряет экран, а не место в
    /// системе Сатурна, и от разворота вида не зависит.
    /// </summary>
    public class NavChrome : MonoBehaviour
    {
        /// <summary>Наибольшая доля ширины стекла, которую занимает линейка.</summary>
        const float BarWidth = 0.22f;
        /// <summary>Отступ разметки от края стекла, в долях высоты.</summary>
        const float Margin = 0.05f;
        /// <summary>Длина луча опорного направления, в долях высоты.</summary>
        const float ArrowLength = 0.06f;
        /// <summary>Место под подпись у острия луча, в долях высоты: луч может смотреть в край.</summary>
        const float ArrowLabelRoom = 0.05f;
        /// <summary>
        /// Короче этого проекция опорной оси на стекло уже не направление, а шум разворота:
        /// смотрим почти вдоль неё, и стрелка показывала бы куда попало.
        /// </summary>
        const float MinArrowProjection = 0.2f;

        public TextMeshPro labelPrefab;

        LineRenderer bar;
        LineRenderer arrow;
        TextMeshPro barLabel;
        TextMeshPro arrowLabel;
        float lineHeight = 1f;

        void Start()
        {
            bar = CreateLine("ScaleBar");
            arrow = CreateLine("ReferenceArrow");
            barLabel = CreateLabel("ScaleLabel");
            arrowLabel = CreateLabel("ReferenceLabel");
        }

        void LateUpdate()
        {
            NavDisplayPanel display = NavDisplayPanel.instance;
            if (display == null || display.cam == null || barLabel == null) return;
            DrawScaleBar(display);
            DrawReference(display);
        }

        /// <summary>
        /// Линейка круглой длины, а не круглой доли экрана: читают её как «вот столько-то
        /// километров», и число должно быть тем, которое кладут в голову, — иначе мера
        /// требует счёта в уме и ею перестают пользоваться.
        /// </summary>
        void DrawScaleBar(NavDisplayPanel display)
        {
            double across = 2.0 * display.Range * display.cam.aspect;
            double length = NiceLength(across * BarWidth);
            float width = (float)(length / across);
            float tick = (float)display.LabelSceneHeight / (2f * (float)NavScale.OrthographicSize) * 0.5f;

            float left = Margin / display.cam.aspect;
            bar.positionCount = 4;
            bar.SetPosition(0, Point(display, left, Margin + tick));
            bar.SetPosition(1, Point(display, left, Margin));
            bar.SetPosition(2, Point(display, left + width, Margin));
            bar.SetPosition(3, Point(display, left + width, Margin + tick));
            bar.widthMultiplier = display.LineSceneWidth;

            Place(barLabel, display, left, Margin + tick * 2f, TextAlignmentOptions.Left, 0f);
            barLabel.text = $"{length / 1000.0:N0} km";
        }

        /// <summary>
        /// Опорная ось — та, от которой отсчитываются долготы в элементах орбит. Без неё
        /// развёрнутый вид не с чем сличить: картинка одинаково выглядит при любом рыскании.
        /// </summary>
        void DrawReference(NavDisplayPanel display)
        {
            Transform view = display.cam.transform;
            // Ось отсчёта в осях сцены: перестановка Y и Z оставляет сим-X на месте.
            Vector3 reference = Vector3.right;
            Vector2 screen = new(Vector3.Dot(reference, view.right), Vector3.Dot(reference, view.up));
            if (screen.magnitude < MinArrowProjection)
            {
                arrow.enabled = false;
                arrowLabel.enabled = false;
                return;
            }
            arrow.enabled = true;
            arrowLabel.enabled = true;

            screen.Normalize();
            float aspect = display.cam.aspect;
            Vector2 center = new(
                1f - (Margin + ArrowLength + ArrowLabelRoom) / aspect,
                Margin + ArrowLength);
            Vector2 tip = center + new Vector2(screen.x * ArrowLength / aspect, screen.y * ArrowLength);

            arrow.positionCount = 2;
            arrow.SetPosition(0, Point(display, center.x, center.y));
            arrow.SetPosition(1, Point(display, tip.x, tip.y));
            arrow.widthMultiplier = display.LineSceneWidth;

            // Подпись с той стороны от острия, в которую смотрит луч: иначе она ложится
            // на него самого.
            bool left = screen.x < 0f;
            Place(arrowLabel, display, tip.x, tip.y,
                left ? TextAlignmentOptions.Right : TextAlignmentOptions.Left, left ? 1f : 0f);
            arrowLabel.text = "REF";
        }

        static double NiceLength(double meters)
        {
            if (!(meters > 0.0)) return 1.0;
            double power = Math.Pow(10.0, Math.Floor(Math.Log10(meters)));
            double mantissa = meters / power;
            return (mantissa >= 5.0 ? 5.0 : mantissa >= 2.0 ? 2.0 : 1.0) * power;
        }

        void Place(TextMeshPro label, NavDisplayPanel display, float x, float y,
            TextAlignmentOptions align, float pivotX)
        {
            label.alignment = align;
            label.color = NavPalette.Dim(NavPalette.Other, NavPalette.NameLevel);
            label.rectTransform.pivot = new Vector2(pivotX, 0.5f);
            label.transform.rotation = display.cam.transform.rotation;
            label.transform.position = Point(display, x, y);
            label.transform.localScale = Vector3.one * ((float)display.LabelSceneHeight / lineHeight);
        }

        static Vector3 Point(NavDisplayPanel display, float x, float y) =>
            display.cam.ViewportToWorldPoint(new Vector3(x, y, display.cam.nearClipPlane + 100f));

        LineRenderer CreateLine(string name)
        {
            GameObject host = new(name) { layer = gameObject.layer };
            host.transform.SetParent(transform, false);
            LineRenderer line = host.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = SimLine.Material;
            line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Разметка — самая тихая вещь на стекле: она не про обстановку, а про то, как
            // обстановку читать. Поэтому чужой тон и нижняя ступень яркости.
            line.startColor = line.endColor = NavPalette.Dim(NavPalette.Other, NavPalette.LeaderLevel);
            return line;
        }

        TextMeshPro CreateLabel(string name)
        {
            if (labelPrefab == null) return null;
            TextMeshPro label = Instantiate(labelPrefab, transform);
            label.gameObject.name = name;
            label.gameObject.layer = gameObject.layer;
            foreach (Transform child in label.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = gameObject.layer;
            }
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            // Кегль образца нормализуется: высоту строки задаёт прибор долей экрана, а
            // fontSize у TextMeshPro — не высота строки в мировых единицах.
            label.text = "Xg";
            label.ForceMeshUpdate();
            if (label.preferredHeight > 0f) lineHeight = label.preferredHeight;
            label.enabled = true;
            return label;
        }
    }
}
