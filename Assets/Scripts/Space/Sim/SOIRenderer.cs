using UnityEngine;

namespace OuterSpace.Sim
{
    [RequireComponent(typeof(LineRenderer))]
    public class SOIRenderer : MonoBehaviour
    {
        // Компонент, содержащий данные о SOI
        private IHasSOI parent;
        public bool rendered = false;
        private LineRenderer lineRenderer;
        public int segments = 36; // Количество сегментов для круга

        void Start()
        {
            parent = GetComponentInParent<IHasSOI>();
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.enabled = false;
            lineRenderer.useWorldSpace = true;
            lineRenderer.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
        }

        void LateUpdate()
        {
            if (parent != null && NavDisplayPanel.instance != null)
            {
                if (parent.SOI > 0 && parent.SOI < double.PositiveInfinity)
                {
                    DrawSOI();
                }
            }
        }

        // Сфера влияния - сфера, а рисуется окружностью. Окружность в фиксированной плоскости
        // показала бы верный радиус, но неверную форму; развёрнутая к камере, она читается
        // как «сфера такого радиуса» при любом повороте вида. Радиус - истинный, всегда.
        private void DrawSOI()
        {
            lineRenderer.positionCount = segments + 1;
            lineRenderer.enabled = true;
            lineRenderer.widthMultiplier = NavDisplayPanel.instance.LineSceneWidth;

            float radius = (float)NavScale.SceneUnits(parent.SOI, SimView.metersPerSceneUnit);
            Vector3 center = SimView.ToScene(parent.GlobalPosition);
            Transform view = NavDisplayPanel.instance.cam.transform;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / (float)segments * 360f * Mathf.Deg2Rad;
                lineRenderer.SetPosition(i, center + (view.right * Mathf.Cos(angle) + view.up * Mathf.Sin(angle)) * radius);
            }
            rendered = true;
        }
    }
}
