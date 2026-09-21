using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace OuterSpace.Sim
{
    /// <summary>
    /// Подписи, привязанные к точке на траектории: PE, AP, узлы, отсчёты событий и сближений.
    /// Каждая ставится сдвигом от своего маркера и про остальные не знает, а ставят их разные
    /// TrajectoryRenderer — по одному на корабль и на каждый манёвр, — так что договориться
    /// между собой им негде: PE, AP и MT+ садятся в одну точку. Чтобы кто-то мог уступить,
    /// нужен один, кто видит всех, — тот же довод, что и у NavLabelRail.
    ///
    /// На рельсу эти подписи уходить не могут: в отрыве от своей точки «T+00:12:30» ничего
    /// не значит. Поэтому владелец только раздвигает их по вертикали, и ровно настолько,
    /// чтобы строки перестали лежать друг на друге.
    ///
    /// Заявки приходят в LateUpdate рисующих компонентов, поэтому раскладка обязана быть
    /// позже их всех — отсюда порядок исполнения.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class NavPointLabels : MonoBehaviour
    {
        /// <summary>Наименьший просвет между соседними строками, в высотах строки.</summary>
        const float Spacing = 1.15f;

        sealed class Entry
        {
            public Transform Label;
            public float Left;
            public float Right;
            public float Wanted;
            public float Y;
        }

        static NavPointLabels instance;
        static readonly List<Entry> entries = new();

        void OnEnable() => instance = this;

        void OnDisable()
        {
            if (instance == this) instance = null;
            entries.Clear();
        }

        /// <summary>
        /// Заявка на место: подпись уже стоит там, где хочет. Ширина меряется у самой
        /// подписи — угадать её по числу знаков нельзя, а без ширины две строки в разных
        /// концах экрана расталкивали бы друг друга, не пересекаясь.
        /// </summary>
        public static void Request(TextMeshPro label, NavDisplayPanel display)
        {
            if (instance == null) return;
            label.ForceMeshUpdate();
            Transform view = display.cam.transform;
            Vector3 position = label.transform.position;
            // Прямоугольник растёт от точки отсчёта, а она у подписи слева или справа —
            // смотря куда та выровнена. По одному только положению трансформа ширину
            // не отложить.
            float width = label.preferredWidth * label.transform.localScale.x;
            float left = Vector3.Dot(position, view.right) - width * label.rectTransform.pivot.x;
            entries.Add(new Entry
            {
                Label = label.transform,
                Left = left,
                Right = left + width,
                Wanted = Vector3.Dot(position, view.up),
            });
        }

        void LateUpdate()
        {
            NavDisplayPanel display = NavDisplayPanel.instance;
            if (display == null || display.cam == null)
            {
                entries.Clear();
                return;
            }

            float gap = (float)display.LabelSceneHeight * Spacing;
            // Сверху вниз: кто выше, место не уступает — иначе подписи меняются местами от
            // кадра к кадру, стоит их точкам чуть разойтись.
            entries.Sort((a, b) => b.Wanted.CompareTo(a.Wanted));

            Vector3 up = display.cam.transform.up;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                entry.Y = entry.Wanted;
                // Уступив одному соседу, подпись может наехать на следующего, поэтому спуск
                // повторяется, пока свободного места не найдётся.
                for (int attempt = 0; attempt < entries.Count; attempt++)
                {
                    float pushed = float.PositiveInfinity;
                    for (int j = 0; j < i; j++)
                    {
                        Entry placed = entries[j];
                        if (placed.Right < entry.Left || entry.Right < placed.Left) continue;
                        if (Mathf.Abs(entry.Y - placed.Y) >= gap) continue;
                        pushed = Mathf.Min(pushed, placed.Y - gap);
                    }
                    if (float.IsPositiveInfinity(pushed)) break;
                    entry.Y = pushed;
                }
                entry.Label.position += up * (entry.Y - entry.Wanted);
            }
            entries.Clear();
        }
    }
}
