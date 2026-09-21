using UnityEngine;
using DoublePrecision;

namespace OuterSpace.Sim
{
    [RequireComponent(typeof(LineRenderer))]
    public class OrbitRenderer : MonoBehaviour
    {
        // Потолок, а не число точек: сколько их нужно, решает AstroDynamic по кривизне.
        const int MaxPoints = 1024;
        public IHasOrbit parent = null;
        public bool rendered = false;

        private LineRenderer lineRenderer;
        private SpaceObjectMono owner;
        private readonly System.Collections.Generic.List<Vector3d> points = new();
        // Start is called before the first frame update
        void Start()
        {
            parent = GetComponentInParent<IHasOrbit>();
            owner = GetComponentInParent<SpaceObjectMono>();
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.enabled = false;
            lineRenderer.useWorldSpace = true;
            // Толщина линии - в долях высоты экрана прибора: иначе на разных дальностях
            // одна и та же орбита была бы то нитью, то бревном.
            lineRenderer.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
        }
        // LateUpdate, а не FixedUpdate: порядок с SimMono.FixedUpdate не определён,
        // и после смены центрального тела линия отрисовалась бы вокруг старого центра.
        void LateUpdate()
        {
            
            if (parent != null && parent.OrbitParams != null && NavDisplayPanel.instance != null)
            {
                if (!lineRenderer.enabled) lineRenderer.enabled = true;
                lineRenderer.widthMultiplier = NavDisplayPanel.instance.LineSceneWidth;
                // Цвет орбиты — роль её хозяина, а не настройка в образце: выбрали цель, и
                // её орбита должна выделиться, не дожидаясь правки ассета.
                lineRenderer.startColor = lineRenderer.endColor =
                    NavPalette.For(owner == null ? null : owner.spaceObject);
                Vector3[] positions = GetOrbitPoints(parent.OrbitParams);
                lineRenderer.positionCount = positions.Length;
                lineRenderer.SetPositions(positions);
            } else
            {
                lineRenderer.enabled = false;
            }
        }
        // Точки коники строит AstroDynamic — там же, где и для предсказанных дуг: одна и та
        // же геометрия, посчитанная двумя способами, разъезжается ровно в тот день, когда
        // один из них поправят.
        public Vector3[] GetOrbitPoints(OrbitElements elements)
        {
            // Орбита рисуется только там, где она траектория: за сферой влияния корабль
            // пойдёт вокруг другого тела, и продолжать конику за границу — врать. Гипербола
            // иначе обрывалась бы в случайной точке космоса, где кончился запас по асимптоте.
            double limit = AstroDynamic.TrueAnomalyAtRadius(elements, parent.CentralSOI);
            AstroDynamic.SampleConic(elements, -limit, limit, MaxPoints, points);

            Vector3[] scene = new Vector3[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                scene[i] = SimView.ToScene(points[i] + parent.CenterPosition);
            }
            return scene;
        }
    }
}