using System.Text;
using OuterSpace;
using OuterSpace.Sim;
using UnityEngine;

namespace Interior
{
    /// <summary>
    /// Список целей: что корабль вообще знает и на что наведён сейчас. Пока показывает всё
    /// подряд, но каждый объект уже несёт источник знания — убавлять легче, чем добавлять.
    /// </summary>
    [RequireComponent(typeof(ReadoutPanel))]
    public class TargetListPanel : MonoBehaviour
    {
        ReadoutPanel panel;
        readonly StringBuilder builder = new();

        void Awake() => panel = GetComponent<ReadoutPanel>();

        void Update()
        {
            if (!panel.DueThisFrame) return;

            builder.Clear();
            builder.Append("TARGETS\n");
            foreach (SpaceObject body in SimMono.bodies)
            {
                bool selected = ReferenceEquals(body, SimMono.target);
                builder.Append(selected ? "> " : "  ");
                builder.Append(body.GameObject.name.ToUpperInvariant());
                builder.Append('\n');
            }
            if (SimMono.target == null) builder.Append("\nNO TARGET");
            panel.Text = builder.ToString();
        }
    }
}
