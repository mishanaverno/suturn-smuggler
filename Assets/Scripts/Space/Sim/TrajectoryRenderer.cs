using System;
using System.Collections.Generic;
using DoublePrecision;
using Game;
using TMPro;
using UnityEngine;

namespace OuterSpace.Sim
{
    public interface IHasTrajectory
    {
        IReadOnlyList<TrajectoryPatch> Patches { get; }
        IReadOnlyList<CloseApproach> Approaches { get; }
        SpaceObject Target { get; }
    }

    /// <summary>
    /// Цепочка предсказанных дуг на экране прибора. Каждая дуга рисуется вокруг текущего
    /// положения своего центрального тела — так же, как орбита корабля и точка манёвра:
    /// иначе первая дуга уезжала бы от метки манёвра на весь путь, который луна проходит
    /// за время до него. Цена — дуги вокруг движущихся тел показаны относительно этих тел,
    /// а не в абсолютных координатах, и стык дуг на границе сферы влияния виден как разрыв.
    /// </summary>
    public class TrajectoryRenderer : MonoBehaviour
    {
        // Не число точек, а потолок: сколько их на самом деле, решает AstroDynamic.SampleArc
        // по кривизне дуги.
        public const int MaxPointsPerPatch = 1024;
        const string DefaultFontPath = "Fonts & Materials/LiberationSans SDF";
        const int RingSegments = 24;
        static readonly Color ApproachColor = new(1f, 0.85f, 0.3f);

        // Задаются тем, кто вешает компонент: у манёвра свой цвет и звёздочка в начале дуги,
        // у корабля — только цепочка от его нынешнего положения.
        public Color color = new(0.4f, 1f, 0.9f);
        public bool markStart = false;

        IHasTrajectory source;
        Material material;
        readonly List<LineRenderer> lines = new();
        readonly List<Vector3d> points = new();
        TextMeshPro maneuverLabel;
        TextMeshPro approachLabel;
        float labelLineHeight;

        void Start()
        {
            source = GetComponentInParent<IHasTrajectory>();
            material = ResourcesLoader.LoadPrefab("Sim/Orbit").GetComponent<LineRenderer>().sharedMaterial;
            maneuverLabel = CreateLabel("ManeuverLabel");
            approachLabel = CreateLabel("ApproachLabel");
        }

        void LateUpdate()
        {
            NavDisplayMono display = NavDisplayMono.instance;
            IReadOnlyList<TrajectoryPatch> patches = source?.Patches;
            if (display == null || patches == null || patches.Count == 0)
            {
                Hide(0);
                maneuverLabel.enabled = false;
                approachLabel.enabled = false;
                return;
            }

            int used = 0;
            if (markStart)
            {
                Vector3d start = PointOnArc(patches[0], patches[0].StartEpoch);
                used = DrawStar(used, start, PatchColor(0), display);
                ShowLabel(maneuverLabel, $"MT+{Clock(patches[0].StartEpoch - GameMono.instance.Epoch)}", start, display);
            }
            else
            {
                maneuverLabel.enabled = false;
            }
            for (int i = 0; i < patches.Count; i++)
            {
                TrajectoryPatch patch = patches[i];
                DrawArc(Line(used++), patch, PatchColor(i), display);
                used = DrawEndMarker(used, patch, PatchColor(i), display);
            }

            IReadOnlyList<CloseApproach> approaches = source.Approaches;
            if (approaches != null && approaches.Count > 0)
            {
                CloseApproach approach = approaches[0];
                // Метки сближения кладутся на нарисованные линии, а не в абсолютные точки
                // из прогноза: иначе они висели бы в стороне от траектории, по которой
                // игрок их и читает.
                Vector3d ship = PointOnArc(PatchAt(patches, approach.Epoch), approach.Epoch);
                Vector3d target = PointOnOrbit(source.Target, approach.Epoch);
                DrawLink(Line(used++), ship, target, display);
                DrawRing(Line(used++), ship, ApproachColor, display);
                DrawRing(Line(used++), target, ApproachColor, display);
                ShowLabel(approachLabel, ApproachText(approach), target, display);
            }
            else
            {
                approachLabel.enabled = false;
            }
            Hide(used);
        }

        // Дуги различаются яркостью: видно, где траектория переходит к следующему телу.
        Color PatchColor(int index) => Color.Lerp(color, color * 0.45f, index * 0.3f);

        void DrawArc(LineRenderer line, TrajectoryPatch patch, Color color, NavDisplayMono display)
        {
            AstroDynamic.SampleArc(patch.Orbit, patch.StartEpoch, patch.EndEpoch, MaxPointsPerPatch, points);
            Prepare(line, color, display);
            line.positionCount = points.Count;
            Vector3d center = patch.Central.simTransform.GLOBAL_R;
            for (int i = 0; i < points.Count; i++)
            {
                line.SetPosition(i, SimView.ToScene(center + points[i]));
            }
        }

        static Vector3d PointOnArc(TrajectoryPatch patch, double epoch) =>
            patch.Central.simTransform.GLOBAL_R + AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(patch.Orbit, epoch).r;

        static Vector3d PointOnOrbit(SpaceObject obj, double epoch) => obj.IsRoot
            ? obj.simTransform.GLOBAL_R
            : obj.centralBody.simTransform.GLOBAL_R + AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(obj.orbitParams, epoch).r;

        static TrajectoryPatch PatchAt(IReadOnlyList<TrajectoryPatch> patches, double epoch)
        {
            for (int i = 0; i < patches.Count; i++)
            {
                if (epoch <= patches[i].EndEpoch) return patches[i];
            }
            return patches[patches.Count - 1];
        }

        /// <summary>Точка самого манёвра — звёздочка: ни начало дуги, ни событие на ней.</summary>
        int DrawStar(int index, Vector3d position, Color color, NavDisplayMono display)
        {
            Vector3 center = SimView.ToScene(position);
            float radius = (float)display.MarkerSceneDiameter * 0.5f;
            for (int i = 0; i < 3; i++)
            {
                float angle = Mathf.PI * i / 3f;
                Vector3 arm = (display.cam.transform.right * Mathf.Cos(angle)
                    + display.cam.transform.up * Mathf.Sin(angle)) * radius;
                LineRenderer line = Line(index++);
                Prepare(line, color, display);
                line.positionCount = 2;
                line.SetPosition(0, center - arm);
                line.SetPosition(1, center + arm);
            }
            return index;
        }

        /// <summary>Вход в сферу влияния — кружок, выход и столкновение — крестик.</summary>
        int DrawEndMarker(int index, TrajectoryPatch patch, Color color, NavDisplayMono display)
        {
            Vector3d point = PointOnArc(patch, patch.EndEpoch);
            switch (patch.EndReason)
            {
                case PatchEndReason.EnteredSOI:
                    DrawRing(Line(index++), point, color, display);
                    break;
                case PatchEndReason.EscapedSOI:
                case PatchEndReason.Impact:
                    LineRenderer first = Line(index++);
                    LineRenderer second = Line(index++);
                    DrawCross(first, second, point, color, display);
                    break;
            }
            return index;
        }

        // Метки развёрнуты к камере и фиксированного экранного размера: точка события — это
        // момент, а не тело, и собственного размера у неё нет.
        void DrawRing(LineRenderer line, Vector3d position, Color color, NavDisplayMono display)
        {
            Prepare(line, color, display);
            line.positionCount = RingSegments + 1;
            Vector3 center = SimView.ToScene(position);
            Vector3 right = display.cam.transform.right * (float)display.MarkerSceneDiameter * 0.5f;
            Vector3 up = display.cam.transform.up * (float)display.MarkerSceneDiameter * 0.5f;
            for (int i = 0; i <= RingSegments; i++)
            {
                float angle = 2f * Mathf.PI * i / RingSegments;
                line.SetPosition(i, center + right * Mathf.Cos(angle) + up * Mathf.Sin(angle));
            }
        }

        // Одной ломаной крестик не нарисовать: диагонали не соединены.
        void DrawCross(LineRenderer first, LineRenderer second, Vector3d position, Color color, NavDisplayMono display)
        {
            Vector3 center = SimView.ToScene(position);
            Vector3 right = display.cam.transform.right * (float)display.MarkerSceneDiameter * 0.5f;
            Vector3 up = display.cam.transform.up * (float)display.MarkerSceneDiameter * 0.5f;

            Prepare(first, color, display);
            first.positionCount = 2;
            first.SetPosition(0, center - right - up);
            first.SetPosition(1, center + right + up);

            Prepare(second, color, display);
            second.positionCount = 2;
            second.SetPosition(0, center - right + up);
            second.SetPosition(1, center + right - up);
        }

        static void Prepare(LineRenderer line, Color color, NavDisplayMono display)
        {
            line.widthMultiplier = display.LineSceneWidth;
            line.startColor = line.endColor = color;
        }

        void DrawLink(LineRenderer line, Vector3d ship, Vector3d target, NavDisplayMono display)
        {
            Prepare(line, ApproachColor, display);
            line.positionCount = 2;
            line.SetPosition(0, SimView.ToScene(ship));
            line.SetPosition(1, SimView.ToScene(target));
        }

        // Подписи прибора — латиницей: кириллицы в шрифте прибора нет, и TMP молча
        // заменяет её квадратами.
        static string ApproachText(CloseApproach approach) =>
            $"T+{Clock(approach.Epoch - GameMono.instance.Epoch)}  " +
            $"{approach.Distance / 1000.0:F1} km  {approach.RelativeSpeed:F0} m/s";

        void ShowLabel(TextMeshPro label, string text, Vector3d at, NavDisplayMono display)
        {
            label.enabled = true;
            label.text = text;
            float offset = (float)display.MarkerSceneDiameter * 0.5f;
            label.transform.rotation = display.cam.transform.rotation;
            label.transform.position = SimView.ToScene(at)
                + display.cam.transform.rotation * new Vector3(offset, offset, 0f);
            label.transform.localScale = Vector3.one * (float)display.LabelSceneHeight / labelLineHeight;
        }

        static string Clock(double seconds)
        {
            TimeSpan span = TimeSpan.FromSeconds(Math.Max(seconds, 0.0));
            return $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
        }

        LineRenderer Line(int index)
        {
            while (lines.Count <= index) lines.Add(Create());
            LineRenderer line = lines[index];
            line.enabled = true;
            return line;
        }

        void Hide(int from)
        {
            for (int i = from; i < lines.Count; i++) lines[i].enabled = false;
        }

        LineRenderer Create()
        {
            GameObject host = new("Patch");
            host.layer = gameObject.layer;
            host.transform.SetParent(transform, false);
            LineRenderer line = host.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = material;
            line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }

        TextMeshPro CreateLabel(string name)
        {
            GameObject host = new(name);
            host.layer = gameObject.layer;
            host.transform.SetParent(transform, false);
            TextMeshPro text = host.AddComponent<TextMeshPro>();
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null) font = Resources.Load<TMP_FontAsset>(DefaultFontPath);
            text.font = font;
            text.fontSize = 1f;
            // Высота строки меряется на настоящем знаке: у пробела preferredHeight почти
            // нулевой, и масштаб подписи улетает в бесконечность.
            text.text = "0";
            text.alignment = TextAlignmentOptions.Left;
            text.rectTransform.sizeDelta = new Vector2(40f, 2f);
            text.rectTransform.pivot = new Vector2(0f, 0.5f);
            // fontSize у TextMeshPro — не высота строки в мировых единицах, поэтому высота
            // измеряется, а не выводится из константы (как в BodyGlyphMono).
            text.ForceMeshUpdate();
            labelLineHeight = text.preferredHeight;
            text.enabled = false;
            return text;
        }
    }
}
