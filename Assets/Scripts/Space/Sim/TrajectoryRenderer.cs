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
        static readonly Color ApproachColor = new(1f, 0.85f, 0.3f);

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
            ShowLabel(maneuverLabel, $"MT+{Clock(patches[0].StartEpoch - GameMono.instance.Epoch)}",
                PointOnArc(patches[0], patches[0].StartEpoch), display);
            for (int i = 0; i < patches.Count; i++)
            {
                TrajectoryPatch patch = patches[i];
                DrawArc(Line(used++), patch, PatchColor(i), display);

                if (patch.EndReason != PatchEndReason.EnteredSOI && patch.EndReason != PatchEndReason.Impact) continue;
                DrawMarker(Line(used++), PointOnArc(patch, patch.EndEpoch), PatchColor(i), display);
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
                DrawMarker(Line(used++), ship, ApproachColor, display);
                DrawMarker(Line(used++), target, ApproachColor, display);
                ShowLabel(approachLabel, ApproachText(approach), target, display);
            }
            else
            {
                approachLabel.enabled = false;
            }
            Hide(used);
        }

        // Дуги различаются яркостью: игрок видит, где траектория переходит к следующему телу.
        static Color PatchColor(int index) => Color.Lerp(new Color(0.4f, 1f, 0.9f), new Color(0.2f, 0.35f, 0.4f), index * 0.3f);

        void DrawArc(LineRenderer line, TrajectoryPatch patch, Color color, NavDisplayMono display)
        {
            AstroDynamic.SampleArc(patch.Orbit, patch.StartEpoch, patch.EndEpoch, MaxPointsPerPatch, points);
            line.widthMultiplier = display.LineSceneWidth;
            line.startColor = line.endColor = color;
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

        // Метка развёрнута к камере и фиксированного экранного размера: точка события —
        // это момент, а не тело, и собственного размера у неё нет.
        void DrawMarker(LineRenderer line, Vector3d position, Color color, NavDisplayMono display)
        {
            line.widthMultiplier = display.LineSceneWidth;
            line.startColor = line.endColor = color;
            line.positionCount = 5;
            float radius = (float)display.MarkerSceneDiameter * 0.5f;
            Vector3 center = SimView.ToScene(position);
            Vector3 right = display.cam.transform.right * radius;
            Vector3 up = display.cam.transform.up * radius;
            line.SetPosition(0, center + up);
            line.SetPosition(1, center + right);
            line.SetPosition(2, center - up);
            line.SetPosition(3, center - right);
            line.SetPosition(4, center + up);
        }

        void DrawLink(LineRenderer line, Vector3d ship, Vector3d target, NavDisplayMono display)
        {
            line.widthMultiplier = display.LineSceneWidth;
            line.startColor = line.endColor = ApproachColor;
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
